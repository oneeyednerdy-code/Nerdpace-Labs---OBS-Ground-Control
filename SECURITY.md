# Security Policy

## Supported version

The latest GitHub release is the supported beta.

## Reporting

Do not post secrets, stream keys, OAuth tokens, private browser-source URLs, or credentials in public issues.

When reporting a security problem, provide the smallest reproducible description possible and sanitize logs before attaching them.

## Process control

OBS Ground Control can terminate OBS processes. Force-close and restart operations may cause unsaved OBS settings to be lost. Automatic process termination is intentionally limited to states the platform can identify with sufficient confidence.


## Windows elevation

The main application runs with normal user permissions. If Windows denies termination of an elevated OBS instance, OBS Ground Control can request UAC for a short-lived helper mode in the same executable. That helper validates that the requested PID belongs to `obs64` before terminating it.
