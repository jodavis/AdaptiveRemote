#!/usr/bin/env bash
# Git ASKPASS helper: supplies GITHUB_TOKEN for all password prompts.
# Git calls this script with a prompt string; we return the token for
# password prompts and a placeholder for username prompts.
case "$1" in
    Username*) echo "x-token-auth" ;;
    Password*) echo "${GITHUB_TOKEN}" ;;
esac
