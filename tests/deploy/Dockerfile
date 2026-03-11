FROM debian:13

ENV DEBIAN_FRONTEND=noninteractive

# Only install systemd (needed as PID 1) and curl (needed to fetch bats).
# Everything else — gnupg, sudo, python3, rsync, ufw, etc. — must be
# installed by setup.sh itself, same as on a fresh Debian host.
RUN apt-get update && apt-get install -y \
    systemd systemd-sysv dbus \
    curl dos2unix \
    && rm -rf /var/lib/apt/lists/* \
    && rm -f /lib/systemd/system/multi-user.target.wants/* \
             /etc/systemd/system/*.wants/* \
             /lib/systemd/system/local-fs.target.wants/* \
             /lib/systemd/system/sockets.target.wants/*udev* \
             /lib/systemd/system/sockets.target.wants/*initctl* \
             /lib/systemd/system/sysinit.target.wants/systemd-tmpfiles-setup* \
             /lib/systemd/system/systemd-update-utmp*

# Install bats-core (test runner only — not a setup.sh dependency)
RUN curl -fsSL https://github.com/bats-core/bats-core/archive/refs/tags/v1.11.1.tar.gz \
    | tar -xz -C /tmp \
    && /tmp/bats-core-1.11.1/install.sh /usr/local \
    && rm -rf /tmp/bats-core-1.11.1

# Copy deploy scripts and tests, fix Windows line endings
COPY deploy/ /opt/deploy/
COPY tests/deploy/ /opt/tests/deploy/
RUN find /opt/deploy /opt/tests/deploy -type f \( -name '*.sh' -o -name '*.bats' -o -name '*.bash' -o -name 'Caddyfile' -o -name '*.yml' -o -name '*.service' -o -name '*.timer' \) -exec dos2unix {} +

WORKDIR /opt/tests/deploy

STOPSIGNAL SIGRTMIN+3
CMD ["/lib/systemd/systemd"]
