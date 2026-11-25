#!/usr/bin/env bash
# Starts Gitea 1.22.6 fresh on http://127.0.0.1:3001 for the R9 benchmark (tools/Autestiq.Benchmark/GiteaScenarios.cs):
# installation locked, SQLite, one user "gardener" with one public repository "apples" and its README. Every run of
# this script throws the previous instance away. The user's password is random and used only to create the repository.
set -euo pipefail

image="${GITEA_IMAGE:-mirror.gcr.io/gitea/gitea:1.22.6}"
docker rm -f r9-gitea >/dev/null 2>&1 || true
docker run -d --name r9-gitea -p 3001:3000 \
  -e GITEA__security__INSTALL_LOCK=true \
  -e GITEA__database__DB_TYPE=sqlite3 \
  -e GITEA__server__ROOT_URL=http://127.0.0.1:3001/ \
  -e GITEA__server__HTTP_PORT=3000 \
  "$image" >/dev/null

for _ in $(seq 1 60); do
  curl -fs -o /dev/null http://127.0.0.1:3001/ && break
  sleep 2
done

password="$(head -c 24 /dev/urandom | base64 | tr -dc 'A-Za-z0-9' | head -c 20)Aa1!"
docker exec -u git r9-gitea gitea admin user create --username gardener --email gardener@example.test \
  --password "$password" --must-change-password=false >/dev/null
curl -fs -o /dev/null -u "gardener:$password" -H 'Content-Type: application/json' \
  -d '{"name":"apples","description":"Crisp and sweet, picked in October.","auto_init":true,"private":false}' \
  http://127.0.0.1:3001/api/v1/user/repos
echo "Gitea is at http://127.0.0.1:3001 with gardener/apples."
