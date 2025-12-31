# tooFastLotto
High Performance Lotto Simulator in C#

This project is a high performance Lotto 6/49 simulation written in C#.

It uses:
- very fast random number generator (Xoshiro256**)
- multithreading (uses all CPU cores)
- no memory allocations inside loops
- bit masks instead of array comparisons

The goal is to simulate how many draws are needed to hit one exact Lotto set.

---

## How it works

1. One random Lotto set (6 numbers from 1–49) is generated.
2. Many threads draw random sets in parallel.
3. Each set is converted into a bit mask.
4. When masks are equal → hit found.
5. The program prints total draws, time, and speed.

---

## Why this project

- learning high performance C#
- learning multithreading
- learning low level optimizations
- I was bored

---

## Requirements

- .NET 7 or newer
- Any OS (Windows / Linux / macOS)

---

## Performance results

Test machine:
- ThinkPad T480
- Intel i5-8250U
- Arch Linux

Searching: 11 17 20 36 38 48 (mask: 00098428)
Threads: 8 | Batch: 100,000

Hit after 3,975,054 draws
Time: 0.26 s → 15,034,027 draws/s
