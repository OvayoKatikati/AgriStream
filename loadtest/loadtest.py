#!/usr/bin/env python3
"""
Simple concurrent load generator for the AgriStream App Service (no extra packages needed).

Usage:
    python loadtest.py https://<your-app>.azurewebsites.net --users 100 --duration 300

Each simulated user loops over public pages (/, /Account/Login, /health) and /Load/Cpu?ms=200 (a capped
CPU-burning demo endpoint), so the App Service plan's CPU rises above the auto-scale threshold.
Prints a summary and writes results.csv for your report. Run it from your own computer.
"""
import argparse
import csv
import statistics
import threading
import time
import urllib.error
import urllib.request

PATHS = ["/", "/Account/Login", "/health", "/Load/Cpu?ms=200", "/Load/Cpu?ms=200"]


def worker(base_url, deadline, results, lock, user_id):
    i = user_id  # stagger so users do not all hit the same page together
    while time.time() < deadline:
        path = PATHS[i % len(PATHS)]
        i += 1
        started = time.time()
        status = 0
        try:
            with urllib.request.urlopen(base_url + path, timeout=30) as resp:
                resp.read()
                status = resp.status
        except urllib.error.HTTPError as e:
            status = e.code
        except Exception:
            status = 0  # timeout / connection error
        elapsed_ms = (time.time() - started) * 1000
        with lock:
            results.append((time.strftime("%H:%M:%S"), path, status, round(elapsed_ms, 1)))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("url", help="Base URL, e.g. https://st12345.azurewebsites.net")
    parser.add_argument("--users", type=int, default=100, help="Concurrent simulated users")
    parser.add_argument("--duration", type=int, default=300, help="Test length in seconds")
    args = parser.parse_args()

    base_url = args.url.rstrip("/")
    results, lock = [], threading.Lock()
    deadline = time.time() + args.duration

    print(f"Starting {args.users} users against {base_url} for {args.duration}s ...")
    threads = [threading.Thread(target=worker, args=(base_url, deadline, results, lock, n), daemon=True)
               for n in range(args.users)]
    start = time.time()
    for t in threads:
        t.start()
    for t in threads:
        t.join()
    total_time = time.time() - start

    ok = [r for r in results if 200 <= r[2] < 400]
    failed = len(results) - len(ok)
    latencies = sorted(r[3] for r in ok)

    with open("results.csv", "w", newline="") as f:
        writer = csv.writer(f)
        writer.writerow(["time", "path", "status", "elapsed_ms"])
        writer.writerows(results)

    print("\n===== LOAD TEST SUMMARY =====")
    print(f"Total requests : {len(results)}")
    print(f"Successful     : {len(ok)}")
    print(f"Failed         : {failed}")
    print(f"Throughput     : {len(results) / total_time:.1f} req/s")
    if latencies:
        print(f"Avg latency    : {statistics.mean(latencies):.0f} ms")
        print(f"Median latency : {statistics.median(latencies):.0f} ms")
        print(f"95th percentile: {latencies[int(len(latencies) * 0.95) - 1]:.0f} ms")
        print(f"Max latency    : {latencies[-1]:.0f} ms")
    print("Detailed rows saved to results.csv")


if __name__ == "__main__":
    main()
