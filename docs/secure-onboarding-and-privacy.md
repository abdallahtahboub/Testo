# Autestiq — Secure Onboarding, Privacy & Enterprise Architecture

> The owner's architectural requirements, given on 2026-11-17. Binding for all future development
> (CLAUDE.md, "Security, privacy and onboarding"). They are long-term requirements, not an instruction to
> build everything at once: §8 sets the phases, and the gap analysis against the current system is in
> [`secure-onboarding-gap-analysis.md`](secure-onboarding-gap-analysis.md).

## 1. Project vision
Autestiq is an autonomous, AI-powered Quality Assurance SaaS platform being developed primarily with Microsoft
.NET, PostgreSQL, and modern testing technologies.

The platform enables organizations to generate and manage:

- QA strategies and test plans.
- Test suites and individual test cases.
- Automated end-to-end and API tests.
- Application discovery and analysis.
- Test execution, defect reports, and QA documentation.
- AI-assisted test maintenance and continuous quality improvement.

**Core principle:** customer privacy, confidentiality, security, and ownership of their software and data are
non-negotiable architectural priorities. All future development must consider these requirements.

## 2. Critical architectural decision
Autestiq **must not** require customers to upload or expose their entire source-code repositories to use the
platform. Source-code access must remain strictly optional, explicitly authorized, and limited to what is
necessary.

Autestiq must support multiple onboarding methods:

1. **Staging URL:** authorized application discovery and black-box testing without source-code access.
2. **Private testing agent:** customer-hosted testing inside the customer's network or infrastructure.
3. **API specifications:** import approved OpenAPI, GraphQL schemas, and related documentation.
4. **Requirements and documentation:** analyze user stories, acceptance criteria, business requirements, and
   architecture documents.
5. **Optional repository integration:** explicitly authorized, least-privilege access to selected repositories
   for advanced analysis.

The architecture must allow these methods to be used independently or combined.

## 3. Separation of control and execution
Design Autestiq around two logical components:

- **SaaS control plane:** organizations, authentication, project management, test plans, test orchestration,
  policy configuration, and approved reporting.
- **Testing execution plane:** authorized application discovery, browser automation, API testing, execution,
  and evidence collection.

The execution plane must support customer-controlled infrastructure. A private agent should:

- execute tests locally using approved tools such as Playwright;
- connect to Autestiq through authenticated outbound communication;
- enforce authorized application and network boundaries;
- retrieve credentials from approved customer-side secret storage;
- keep sensitive test artifacts local by default;
- transmit only information permitted by the customer's data-sharing policy;
- support revocation, versioning, auditable actions, and resource limits.

A network tunnel to a cloud-hosted browser must not be represented as equivalent to fully private execution.

## 4. AI privacy and data protection
All AI features must be designed around data minimization and explicit processing permissions. The system must
distinguish between:

- public or approved non-sensitive metadata;
- customer-confidential application information;
- credentials and authentication material;
- personal or regulated data;
- sensitive execution evidence, including screenshots, DOM content, traces, logs, and API responses.

Never assume staging environments contain harmless data. Never automatically transmit confidential customer
information to external AI providers.

Support an architecture capable of evolving toward:

- managed cloud AI processing;
- private execution with policy-restricted cloud AI processing;
- fully customer-controlled AI inference and execution.

Do not claim that private execution automatically guarantees private AI processing.

## 5. Secure-by-design development rules
All future implementations must consider:

- strong multi-tenant isolation and organization-scoped authorization;
- least-privilege access and role-based access controls;
- secure secret handling and encryption;
- environment authorization and destination allowlisting;
- SSRF, DNS rebinding, prompt-injection, and malicious-content defenses;
- safe browser and API execution boundaries;
- audit logging and traceable agent actions;
- controlled data retention and deletion;
- configurable evidence storage and transmission policies;
- GDPR-relevant privacy and contractual requirements;
- explicit approval for destructive or state-changing testing operations.

Security policies must be enforced by deterministic software controls, not solely through AI prompts.
Production testing must be disabled by default unless the customer explicitly authorizes and configures it.

## 6. Application understanding and QA intelligence
Autestiq must be capable of generating useful QA artifacts without source-code access. The
application-understanding engine should combine authorized evidence from:

- observed UI and API behavior;
- API specifications;
- requirements and acceptance criteria;
- architecture documentation;
- optional source-code analysis.

The AI must distinguish:

1. observed application behavior;
2. documented expected behavior;
3. AI-generated assumptions requiring verification.

Never present inferred business rules as verified requirements. Generated tests should be traceable to their
source evidence, with uncertainty clearly identified.

## 7. Customer onboarding experience
The user experience should be simple, secure, and progressive. Organizations should be able to:

1. create a testing project;
2. select their application type;
3. select an authorized connection method;
4. configure environment and data-processing policies;
5. provide optional specifications and requirements;
6. review the discovery scope and permissions;
7. approve discovery and testing activities;
8. review generated QA strategies and tests;
9. execute authorized tests;
10. inspect approved evidence and reports.

Do not require unnecessary access or excessive setup for basic functionality.

## 8. Implementation priorities
Treat this document as a long-term architectural requirement, **not** an instruction to implement every feature
immediately. Use incremental development:

- **Phase 1:** secure web/API onboarding, limited discovery, QA artifact generation, and strict authorization
  boundaries.
- **Phase 2:** customer-hosted private execution agent and controlled evidence reporting.
- **Phase 3:** enhanced application understanding through specifications and requirements.
- **Phase 4:** optional repository integration, CI/CD integration, and intelligent regression analysis.
- **Phase 5:** advanced enterprise features, including fully private AI processing and deployment options.

These phases are proposed priorities, not authorization to rewrite the existing roadmap. Preserve functioning
features and avoid unnecessary refactoring.

## 9. Mandatory engineering workflow
Before implementing any feature affecting application access, AI processing, testing, infrastructure, or
customer data:

1. Examine the existing architecture and relevant implementation.
2. Identify affected trust boundaries and potential security risks.
3. Check consistency with these architectural principles.
4. Prefer the simplest implementation that preserves future extensibility.
5. Define acceptance criteria, including negative and security test cases.
6. Implement appropriate automated tests.
7. Document architectural decisions and technical tradeoffs.
8. Identify any temporary limitations or deferred security capabilities.

Never silently weaken security requirements to accelerate implementation. If a requested feature conflicts with
these principles, explain the conflict and propose a safer alternative. Do not introduce major architecture
changes or new infrastructure dependencies without explaining their necessity.

## 10. Immediate instruction (2026-11-17)
First inspect the current repository, project architecture, existing milestones, and any established
instructions. Compare the current implementation against these requirements. Identify already implemented
capabilities, missing capabilities, existing architectural risks, security and privacy weaknesses, and changes
necessary now versus changes that can safely wait. Produce a prioritized gap analysis and implementation
recommendations. Do not immediately restructure the repository, implement the complete architecture, or modify
working functionality. Wait for explicit approval before undertaking major architectural changes.

*Done: [`secure-onboarding-gap-analysis.md`](secure-onboarding-gap-analysis.md).*

## Final principle
Autestiq must be useful without source-code access, more capable with explicitly authorized context, and capable
of private execution for organizations with strict confidentiality requirements. Customer trust and
secure-by-design engineering take priority over development speed, feature quantity, and autonomous AI
capabilities.
