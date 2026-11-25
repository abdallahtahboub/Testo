#!/usr/bin/env bash
# Starts OWASP Juice Shop 17.1.1 fresh on http://127.0.0.1:3000 for the R9 benchmark
# (tools/Autestiq.Benchmark/JuiceShopScenarios.cs). Its database is rebuilt when the container starts.
set -euo pipefail

docker rm -f r9-juice-shop >/dev/null 2>&1 || true
docker run -d --name r9-juice-shop -p 3000:3000 "${JUICE_SHOP_IMAGE:-bkimminich/juice-shop:v17.1.1}" >/dev/null

for _ in $(seq 1 60); do
  curl -fs -o /dev/null http://127.0.0.1:3000/ && { echo "Juice Shop is at http://127.0.0.1:3000."; exit 0; }
  sleep 2
done

echo "Juice Shop did not start." >&2
exit 1
