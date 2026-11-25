# The worker image (ADR-0026): the worker of a sealed run, or its outer proxy with --proxy.
#
# Built from the published worker, so the image carries no compiler and needs no package feed:
#   dotnet publish src/Autestiq.Worker -c Release -o artifacts/worker -p:UseAppHost=false
#   docker build -f src/Autestiq.Worker/Dockerfile -t autestiq-worker artifacts/worker

# Playwright's own image, at the version of Microsoft.Playwright in Directory.Packages.props, so that the browser
# it carries is the build the package expects. It also has the browser's system libraries and a non-root user.
FROM mcr.microsoft.com/playwright:v1.64.1-noble
COPY --from=mcr.microsoft.com/dotnet/runtime:10.0 /usr/share/dotnet /usr/share/dotnet
COPY . /app

# Playwright marks its bundled driver executable the first time it runs, which a read-only root does not allow.
RUN chmod 0755 /app/.playwright/node/linux-x64/node

ENV DOTNET_ROOT=/usr/share/dotnet \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    AUTESTIQ_BROWSER_PATH=/ms-playwright/chromium_headless_shell-1248/chrome-headless-shell-linux64/chrome-headless-shell \
    HOME=/tmp

USER pwuser
ENTRYPOINT ["/usr/share/dotnet/dotnet", "/app/Autestiq.Worker.dll"]
