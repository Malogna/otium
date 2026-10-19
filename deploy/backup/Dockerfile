# Backup sidecar: sqlite3 for online backups, rclone for optional off-site copies.
FROM alpine:3.24

RUN apk add --no-cache sqlite rclone \
    && mkdir -p /backups \
    && chown 1654:1654 /backups

COPY --chmod=755 backup.sh /usr/local/bin/otium-backup

# The server image's "app" user; no home directory, so rclone keeps its scratch files in /tmp.
ENV HOME=/tmp
USER 1654:1654
VOLUME /backups

ENTRYPOINT ["otium-backup"]
CMD ["loop"]
