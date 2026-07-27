# Curated release notes

`.github/workflows/release.yml` looks for a file named after the release tag, e.g. `v2.34.1.2` ->
`2.34.1.2.md`, and uses it as the GitHub release body verbatim. The tag is a release train's label,
not either package's own version — `OpenTok.Net.Android` and `OpenTok.Net.webrtc.Dependency.Android`
publish independently, each at its own pin from `Directory.Build.props`. When no such file exists,
the workflow falls back to a generated `git log` summary since the previous tag.

Add a file here before tagging a release you want curated notes for; otherwise the generated
changelog is fine.
