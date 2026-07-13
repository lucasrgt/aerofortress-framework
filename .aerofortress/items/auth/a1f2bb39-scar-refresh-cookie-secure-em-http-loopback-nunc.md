---
id: a1f2bb39-b3cf-42e1-8ca0-8984573ed4e4
slug: auth
type: scar
title: SCAR: refresh cookie Secure em HTTP loopback nunca retorna ao servidor
tags: auth, refresh-token, cookie, localhost, pilots
provenance: observado
evidence: src/AeroFortress.Framework.Auth/RefreshCookie.cs; tests/AeroFortress.Framework.Auth.Tests/RefreshCookieTests.cs
decay: stable
created: 2026-07-13T04:39:14.030803600+00:00
updated: 2026-07-13T04:39:14.030803600+00:00
validated: 2026-07-13T04:39:14.030803600+00:00
links:
---

Em todos os pilots web locais, o login emitia o refresh cookie, mas a primeira rotação devolvia `auth.invalid_session`: `RefreshCookie.SetRefresh` marcava `Secure` incondicionalmente enquanto Vite rodava em `http://localhost`, então o navegador não reenviava a credencial. A cura framework-first é omitir `Secure` somente quando a requisição é HTTP e o host é loopback (`localhost`, 127/8 ou ::1); HTTPS local e qualquer host não-loopback continuam Secure, inclusive atrás de TLS termination. O blueprint também deve escopar o cookie ao path público do browser `/api/account`, não ao path interno `/account`. Testar duas rotações consecutivas pelo proxy web, não apenas o header Set-Cookie.
