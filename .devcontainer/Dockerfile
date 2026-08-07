FROM mcr.microsoft.com/devcontainers/base:noble

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        clang \
        llvm \
        zlib1g-dev \
    && rm -rf /var/lib/apt/lists/*
