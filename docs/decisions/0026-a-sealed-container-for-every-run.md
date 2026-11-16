# 0026 — A sealed container for every run, with the proxy outside it

- **Status:** Proposed
- **Date:** 2026-11-16

## Context
ADR-0018 decided that browser automation runs in a container per run, non-root, and that "the worker's
network namespace has one route out: a proxy". What was built in Milestone 3.3 is a separate **process**
on the host. Its egress proxy lives **inside that process**, so the policy is enforced by the very code
a hostile page is assumed to own: a browser exploit that reaches code execution could open its own socket
and go anywhere the host can. The threat model makes the container gate binding before any
customer-supplied target, and Milestone 3.5 step 5 is that gate. The owner approved Docker for it on
2026-11-16.

Measured on this repository's development container, Docker 29.9 (2026-11-16):

| A container on… | the host's own address on that network | another Docker network's host address | the internet (1.1.1.1:443) | cloud metadata (169.254.169.254) | a peer container on the same network |
|---|---|---|---|---|---|
| an `--internal` network | **reachable** | blocked | blocked | blocked | reachable |
| an `--internal` network with `inhibit_ipv4` | blocked | blocked | blocked | blocked | reachable |

So `--internal` alone is not enough: it leaves every host service that listens on all interfaces, a
database for one, one hop away. With `com.docker.network.bridge.inhibit_ipv4=true` the host has no address
on that network, and the only thing a container can reach is a peer on the same network.

Playwright's own image `mcr.microsoft.com/playwright:v1.64.1-noble` carries exactly the browser build
(`chromium_headless_shell-1248`) that our `Microsoft.Playwright` 1.64.1 expects, and a non-root user
(`pwuser`, uid 1001). The .NET 10 runtime image pulls and runs.

## Proposal
For each run, the dispatcher:

1. **Creates a sealed network for the run alone**: `--internal`, with `inhibit_ipv4`. No route out, no
   host address, nothing of another run's on it.
2. **Starts the outer proxy** as a container of the worker image in proxy mode, attached to the sealed
   network and to the default bridge. It enforces the same `EgressPolicy` as today, resolving every name
   itself and refusing private, loopback, link-local, metadata and their IPv6 forms. Nothing is
   trusted-local in container mode, so the Docker host's address on the bridge is refused like any other
   private address.
3. **Starts the worker** in a container on the sealed network only, hardened:
   - non-root (`pwuser`), `--cap-drop ALL`, `--security-opt no-new-privileges`;
   - read-only root file system, with a small `tmpfs` for `/tmp` and the browser's profile;
   - limits on memory, CPU and process count;
   - no environment credentials;
   - one writable mount: the run's workspace, for its screenshots.
4. **Keeps the worker's own proxy, chained to the outer one.** The browser still talks to the in-process
   proxy, which still checks the origin allowlist and counts bytes for the budget, but it no longer
   resolves names or connects anywhere itself: it opens a tunnel through the outer proxy, which is the
   only thing the worker can reach. The policy is enforced twice, and the one that matters is outside
   the code a hostile page could own.
5. **Removes all three** when the run ends, however it ends. The stuck-run clean-up (step 3) also
   removes anything a lost dispatcher left behind, found by a label on each container and network.

**Two modes, chosen by configuration** (`Execution:Isolation`):
- **`Container`**: required for any customer target. `MayTargetAsync` refuses an origin that is not one
  of ours unless the dispatcher is in this mode, so a proof of control is not enough on its own.
- **`Process`**: today's behaviour, kept for development, the test suite and the benchmark. They run our
  own applications on loopback, which the sealed design deliberately cannot reach.

**Tests.**
- *In process, no Docker:* the chained proxies, two `EgressProxy` instances; the outer one refuses what
  the policy refuses; the inner one opens no connection of its own.
- *With Docker, on CI, which has it:* the gate tests the threat model asks for, run from inside a
  worker container started the way the dispatcher starts one. Each must hold:
  - the Docker host and the database cannot be reached;
  - the metadata address and the internet cannot be reached directly;
  - the root file system cannot be written;
  - the process is not root;
  - no credential is in the environment.
- *The end-to-end test:* a dispatcher in container mode carrying out a run, then removing its
  containers and network.

Developers who run those tests locally need Docker, as the owner accepted.

## Consequences
- **Easier:** the policy holds even if the browser is fully compromised; a run cannot see another run;
  the gate the threat model sets before customer targets closes.
- **Harder:**
  - A run starts more slowly: one network and two containers instead of one process. To be measured;
    expected to be seconds.
  - The dispatcher needs the Docker API. On the host that means access to the Docker socket, which is
    root-equivalent, so the dispatcher's host is a privileged place and must be treated as one.
  - Our own applications on loopback cannot be tested in container mode, by design. A future
    self-hosted demo target would run as a container on the run's network, not on the host.
- **Must remember:**
  - `inhibit_ipv4` is what keeps the host out. A Docker version that drops it, or a network created
    without it, silently reopens the host, so a gate test checks it on every CI run.
  - Container mode is the only mode in which a customer target may be reached. Process mode must never
    be used in production once customer targets exist.

## Alternatives considered
- **Proxy inside the worker container only.** Rejected: the policy would again be enforced by code in
  the compromised process, which is the problem this ADR exists to fix.
- **Proxy as a process on the host, listening on the run network's gateway.** Rejected: it needs the host
  to have an address on the run network, which the measurement shows is exactly what exposes the host's
  other services. Blocking that needs firewall rules managed by the dispatcher.
- **Host firewall rules instead of `inhibit_ipv4`.** Workable, but it needs `iptables` access and
  per-host configuration. The network option needs neither, and is checked by a test.
- **gVisor or a microVM (Firecracker) per run.** Stronger isolation of the kernel, and more to operate.
  Worth revisiting before untrusted customers at scale; not needed to close this gate.
