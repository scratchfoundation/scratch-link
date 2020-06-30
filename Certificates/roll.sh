#!/bin/bash
echo "IV=\"`hexdump -n 16 -e '4/4 "%08X"' /dev/random`\""
echo "KEY=\"`hexdump -n 32 -e '8/4 "%08X"' /dev/random`\""
