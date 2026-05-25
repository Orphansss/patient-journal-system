#!/bin/bash
# Generates a cryptographically secure 32-byte AES-256 key (base64-encoded)
# Use this output as the value for Encryption:Key in appsettings.Development.json
# In production: store in Azure Key Vault, HashiCorp Vault or similar - NEVER in appsettings.json

echo "New AES-256 key (base64):"
openssl rand -base64 32
echo ""
echo "New JWT key (64 chars):"
openssl rand -hex 32
