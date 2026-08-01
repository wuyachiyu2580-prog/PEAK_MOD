# Changelog

## 0.1.1 - 2026-07-30

- Added account sign-in, sign-out, automatic access-token refresh, and account-owned map management.
- Persisted refresh tokens are protected with Windows DPAPI; access tokens remain memory-only and passwords are never saved.
- Added map editing, JSON replacement, cover replacement/removal, deletion confirmation, and upload ownership.
- Added like state synchronization, map revision metadata, and community detail dialogs.
- Added URL-keyed cover image caching to avoid downloading unchanged images repeatedly.
- Improved local map selection, description editing, cover selection, and input isolation between modal layers.
- Made minor UI adjustments for spacing, readability, and modal presentation.
- Added client support for the deployed `POST /api/auth/sign-out` endpoint to revoke the remote session.

## 0.1.0 - 2026-06-07

- Added in-game map browser for peakmap.top.
- Added JSON download and upload support.
- Added local Map Saves scanning.
- Added Chinese/English UI switching.
