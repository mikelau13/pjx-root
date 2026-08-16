{{/*
Fully-qualified image reference.

Tag precedence: per-service image.tag → global.imageTag → .Chart.AppVersion, so a
chart release and its images move together by default while a single --set can
override everything.

An EMPTY global.imageRegistry must emit a bare name, not a leading slash. Phase 7b
relies on this: k3d-imported images are referenced as `pjx-root-pjx-web-react:latest`
with no registry, and `/pjx-root-pjx-web-react:latest` is not a valid reference.
*/}}
{{- define "pjx.image" -}}
{{- $registry := .root.Values.global.imageRegistry | default "" -}}
{{- $tag := .svc.image.tag | default .root.Values.global.imageTag | default .root.Chart.AppVersion -}}
{{- if $registry -}}
{{- printf "%s/%s:%s" $registry .svc.image.repository $tag -}}
{{- else -}}
{{- printf "%s:%s" .svc.image.repository $tag -}}
{{- end -}}
{{- end -}}
