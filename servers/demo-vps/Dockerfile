FROM ubuntu:24.04

ARG DEMO_USER=kelpie
ARG DEMO_PASSWORD=kelpie-demo

ENV DEBIAN_FRONTEND=noninteractive
ENV DEMO_USER=${DEMO_USER}
ENV DEMO_PASSWORD=${DEMO_PASSWORD}

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates \
        curl \
        iproute2 \
        iputils-ping \
        less \
        net-tools \
        openssh-server \
        procps \
        sudo \
        vim-tiny \
    && rm -rf /var/lib/apt/lists/*

RUN useradd --create-home --shell /bin/bash --groups sudo "${DEMO_USER}" \
    && printf '%s:%s\n' "${DEMO_USER}" "${DEMO_PASSWORD}" | chpasswd \
    && mkdir -p /run/sshd /etc/ssh/sshd_config.d \
    && chmod 0755 /run/sshd

RUN printf '%s\n' \
        'PasswordAuthentication yes' \
        'PubkeyAuthentication yes' \
        'PermitRootLogin no' \
        'UsePAM yes' \
        'X11Forwarding no' \
        'AllowTcpForwarding yes' \
        'KexAlgorithms curve25519-sha256,curve25519-sha256@libssh.org,diffie-hellman-group14-sha256' \
        'HostKeyAlgorithms ssh-ed25519,ecdsa-sha2-nistp256,rsa-sha2-512,rsa-sha2-256' \
        'ClientAliveInterval 60' \
        'ClientAliveCountMax 3' \
        > /etc/ssh/sshd_config.d/kelpie-demo.conf

COPY entrypoint.sh /usr/local/bin/kelpie-demo-entrypoint
RUN chmod 0755 /usr/local/bin/kelpie-demo-entrypoint

EXPOSE 22

ENTRYPOINT ["/usr/local/bin/kelpie-demo-entrypoint"]
CMD ["/usr/sbin/sshd", "-D", "-e"]
