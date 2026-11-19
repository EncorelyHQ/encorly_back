#!/usr/bin/env bash
# vm-encorely-db: disco de datos en /var/lib/postgresql, swap y PostgreSQL 16 solo en la red privada.
set -euo pipefail

DISK=/dev/disk/azure/scsi1/lun0
[ -e "$DISK" ] || DISK=$(readlink -f /dev/disk/by-path/*lun-0 2>/dev/null | head -1)

if ! blkid "$DISK" >/dev/null 2>&1; then
  mkfs.ext4 -q -L pgdata "$DISK"
fi
install -d /var/lib/postgresql
grep -q 'LABEL=pgdata' /etc/fstab || echo 'LABEL=pgdata /var/lib/postgresql ext4 defaults,nofail,noatime 0 2' >> /etc/fstab
mountpoint -q /var/lib/postgresql || mount /var/lib/postgresql

if [ ! -f /swapfile ]; then
  fallocate -l 1G /swapfile && chmod 600 /swapfile && mkswap -q /swapfile && swapon /swapfile
  echo '/swapfile none swap sw 0 0' >> /etc/fstab
fi

export DEBIAN_FRONTEND=noninteractive
apt-get update -qq
apt-get upgrade -y -qq
apt-get install -y -qq postgresql-16 unattended-upgrades

CONF=/etc/postgresql/16/main
sed -i "s/^#\?listen_addresses.*/listen_addresses = 'localhost,10.0.2.4'/" $CONF/postgresql.conf
# Perfil para 1 GB de RAM
cat > $CONF/conf.d/encorely.conf <<'EOF'
shared_buffers = 256MB
effective_cache_size = 640MB
work_mem = 4MB
maintenance_work_mem = 64MB
max_connections = 50
password_encryption = scram-sha-256
EOF
grep -q 'encorely_app 10.0.1.0/24' $CONF/pg_hba.conf || \
  echo 'hostssl encorely encorely_app 10.0.1.0/24 scram-sha-256' >> $CONF/pg_hba.conf
systemctl restart postgresql

sudo -u postgres psql -tAc "SELECT 1 FROM pg_roles WHERE rolname='encorely_app'" | grep -q 1 || \
  sudo -u postgres psql -qc 'CREATE USER encorely_app;'
sudo -u postgres psql -tAc "SELECT 1 FROM pg_database WHERE datname='encorely'" | grep -q 1 || \
  sudo -u postgres psql -qc 'CREATE DATABASE encorely OWNER encorely_app;'

echo "ssl=$(sudo -u postgres psql -tAc 'SHOW ssl;') listen=$(sudo -u postgres psql -tAc 'SHOW listen_addresses;') data=$(df -h /var/lib/postgresql | awk 'NR==2{print $2}')"
