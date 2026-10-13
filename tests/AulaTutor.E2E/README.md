# E2E

Sin runner E2E adicional ni navegadores instalados. Smoke.ps1 verifica salud/proxy;
Http-Identity.ps1 verifica identidad/tutoría y los recorridos A/B/C de aprendizaje
con HTTPS/PostgreSQL reales, Mock y datos ficticios. La revisión visual utiliza
Edge existente y Playwright incluido en el runtime, con evidencia en QA_4.md.

`Http-Identity.ps1 -KeepRunning -SetupOnly` prepara exclusivamente el entorno
aislado de identidad para QA visual; **no cuenta como verificación A/B/C**.
Sin SetupOnly se ejecutan los recorridos completos, con pausas para respetar la
cuota de seis envíos por minuto. Limpiar proceso/base/http-qa.json al terminar.
