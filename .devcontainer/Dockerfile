FROM mcr.microsoft.com/devcontainers/base:ubuntu-24.04
# Includes: vscode user (UID 1000) with passwordless sudo, git, curl, wget, jq, etc.

# 1. GitHub CLI
RUN curl -fsSL https://cli.github.com/packages/githubcli-archive-keyring.gpg \
        -o /usr/share/keyrings/githubcli-archive-keyring.gpg \
    && chmod go+r /usr/share/keyrings/githubcli-archive-keyring.gpg \
    && echo "deb [arch=$(dpkg --print-architecture) signed-by=/usr/share/keyrings/githubcli-archive-keyring.gpg] \
        https://cli.github.com/packages stable main" \
        > /etc/apt/sources.list.d/github-cli.list \
    && apt-get update && apt-get install -y --no-install-recommends gh \
    && rm -rf /var/lib/apt/lists/*

# 2. .NET 10 SDK + PowerShell
#    global.json pins sdk.version 10.0.100 with rollForward:latestFeature.
#    pwsh is required to run playwright.ps1 on Linux.
RUN curl -fsSL https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb \
        -o /tmp/packages-microsoft-prod.deb \
    && dpkg -i /tmp/packages-microsoft-prod.deb && rm /tmp/packages-microsoft-prod.deb \
    && apt-get update \
    && apt-get install -y --no-install-recommends dotnet-sdk-10.0 powershell \
    && rm -rf /var/lib/apt/lists/*

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1

# 3. Playwright OS-level Chromium dependencies
#    Baked into the image so the heavy apt work is cached.
#    playwright.ps1 install --with-deps in post-create will be a no-op for these.
#    Ubuntu 24.04 renamed libasound2 → libasound2t64.
RUN apt-get update && apt-get install -y --no-install-recommends \
    libnss3 libnspr4 libatk1.0-0 libatk-bridge2.0-0 libcups2 libdrm2 libdbus-1-3 \
    libxkbcommon0 libatspi2.0-0 libx11-6 libxcomposite1 libxdamage1 libxext6 \
    libxfixes3 libxrandr2 libgbm1 libpango-1.0-0 libcairo2 libasound2t64 \
    libxshmfence1 fonts-liberation fonts-noto-color-emoji xdg-utils \
    && rm -rf /var/lib/apt/lists/*

# 4. Node.js LTS (for Claude Code CLI)
#    Explicit keyring + pinned node_24.x instead of curl|bash for auditability.
RUN curl -fsSL https://deb.nodesource.com/gpgkey/nodesource-repo.gpg.key \
        | gpg --dearmor -o /usr/share/keyrings/nodesource.gpg \
    && echo "deb [arch=$(dpkg --print-architecture) signed-by=/usr/share/keyrings/nodesource.gpg] \
        https://deb.nodesource.com/node_24.x nodistro main" \
        > /etc/apt/sources.list.d/nodesource.list \
    && apt-get update \
    && apt-get install -y --no-install-recommends nodejs \
    && rm -rf /var/lib/apt/lists/*

# 5. Claude Code CLI (global npm install, accessible to all users)
RUN npm install -g @anthropic-ai/claude-code

# 6. Python ML stack in isolated venv at /opt/ml-env
#    World-readable so the vscode user can use python/pip from it.
#    python3-venv is a separate package from python3 on Debian/Ubuntu.
COPY ml/requirements.txt /tmp/ml-requirements.txt
RUN apt-get update \
    && apt-get install -y --no-install-recommends python3-venv \
    && rm -rf /var/lib/apt/lists/* \
    && python3 -m venv /opt/ml-env \
    && /opt/ml-env/bin/pip install --upgrade pip setuptools wheel \
    && /opt/ml-env/bin/pip install -r /tmp/ml-requirements.txt \
    && /opt/ml-env/bin/pip cache purge \
    && rm /tmp/ml-requirements.txt \
    && chmod -R a+rX /opt/ml-env

RUN echo 'export PATH="/opt/ml-env/bin:$PATH"' >> /etc/profile.d/ml-env.sh \
    && chmod +x /etc/profile.d/ml-env.sh
ENV PATH="/opt/ml-env/bin:$PATH"

USER vscode

# Create ~/.claude as vscode so Docker initialises the named volume with the
# correct ownership. Without this the volume root is owned by root and the
# vscode user cannot write auth credentials after login.
RUN mkdir -p /home/vscode/.claude

WORKDIR /workspaces/AdaptiveRemote
SHELL ["/bin/bash", "-l", "-c"]
