FROM mcr.microsoft.com/devcontainers/base:noble

ARG TARGETARCH

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates \
        clang \
        curl \
        ffmpeg \
        file \
        libasound2t64 \
        libatk-bridge2.0-0t64 \
        libatk1.0-0t64 \
        libatspi2.0-0t64 \
        libcairo2 \
        libcups2t64 \
        libdbus-1-3 \
        libdrm2 \
        libgbm1 \
        libglib2.0-0t64 \
        libnspr4 \
        libnss3 \
        libpango-1.0-0 \
        libx11-6 \
        libxcb1 \
        libxcomposite1 \
        libxdamage1 \
        libxext6 \
        libxfixes3 \
        libxkbcommon0 \
        libxrandr2 \
        llvm \
        zlib1g-dev \
    && case "$TARGETARCH" in \
        amd64) \
            vhs_arch=x86_64; \
            vhs_sha=99cb634587eaae0473c1ea377db80c3a048c27f99fe0a7febb1a1e8cb7ee5009; \
            ttyd_arch=x86_64; \
            ttyd_sha=8a217c968aba172e0dbf3f34447218dc015bc4d5e59bf51db2f2cd12b7be4f55 ;; \
        arm64) \
            vhs_arch=arm64; \
            vhs_sha=af782cddbf844a377df6ea41c0e72339393fa021be3f6cb70a2f47d48675d92b; \
            ttyd_arch=aarch64; \
            ttyd_sha=b38acadd89d1d396a0f5649aa52c539edbad07f4bc7348b27b4f4b7219dd4165 ;; \
        *) echo "Unsupported architecture: $TARGETARCH" >&2; exit 1 ;; \
       esac \
    && curl --fail --location --silent --show-error \
        "https://github.com/charmbracelet/vhs/releases/download/v0.11.0/vhs_0.11.0_Linux_${vhs_arch}.tar.gz" \
        --output /tmp/vhs.tar.gz \
    && echo "${vhs_sha}  /tmp/vhs.tar.gz" | sha256sum --check --strict \
    && tar --extract --gzip --file /tmp/vhs.tar.gz --strip-components 1 \
        --directory /usr/local/bin "vhs_0.11.0_Linux_${vhs_arch}/vhs" \
    && curl --fail --location --silent --show-error \
        "https://github.com/tsl0922/ttyd/releases/download/1.7.7/ttyd.${ttyd_arch}" \
        --output /usr/local/bin/ttyd \
    && echo "${ttyd_sha}  /usr/local/bin/ttyd" | sha256sum --check --strict \
    && chmod 0755 /usr/local/bin/vhs /usr/local/bin/ttyd \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/* /tmp/vhs.tar.gz
