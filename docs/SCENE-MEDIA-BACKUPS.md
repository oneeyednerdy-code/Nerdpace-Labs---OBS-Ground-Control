# Scene Media Backups

Streamer Mission Control can optionally include local images, video, and audio referenced by OBS scene collections.

Media backup is disabled by default. The app estimates file count/size first, deduplicates repeated references, never downloads remote URLs, and records missing references in the manifest.

Restore is also explicit. By default it restores only media files that are currently missing. Overwriting an existing local media file requires a second checkbox.

Supported media includes common PNG/JPEG/GIF/WebP/BMP/SVG images, MP4/MOV/MKV/AVI/WebM/M4V/TS video, and MP3/WAV/FLAC/OGG/M4A/AAC/Opus audio.
