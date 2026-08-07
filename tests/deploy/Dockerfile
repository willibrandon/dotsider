# syntax=docker/dockerfile:1

FROM debian:13

ARG DEBIAN_FRONTEND=noninteractive

RUN apt-get update && apt-get install -y --no-install-recommends \
    dbus \
    systemd \
    systemd-sysv \
    && rm -rf /var/lib/apt/lists/* \
    && rm -f /lib/systemd/system/multi-user.target.wants/* \
             /etc/systemd/system/*.wants/* \
             /lib/systemd/system/local-fs.target.wants/* \
             /lib/systemd/system/sockets.target.wants/*udev* \
             /lib/systemd/system/sockets.target.wants/*initctl* \
             /lib/systemd/system/sysinit.target.wants/systemd-tmpfiles-setup* \
             /lib/systemd/system/systemd-update-utmp*

COPY artifacts/deploy-tests/dotsider-deploy-host /opt/test/dotsider-deploy-host
COPY artifacts/deploy-tests/website/ /opt/test/website/
COPY artifacts/deploy-tests/sample/ /opt/test/sample/

RUN chmod 0755 /opt/test/dotsider-deploy-host

STOPSIGNAL SIGRTMIN+3
CMD ["/lib/systemd/systemd"]
