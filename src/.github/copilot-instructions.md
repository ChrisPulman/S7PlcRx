# Copilot Instructions

## Project Guidelines
- The user expects PLC tag reads to be order-independent and reliable regardless of when a value is read, especially after large reads; avoid workflow-only workarounds in the test app.
- For PLC reconnection, switch to a new connection only after the PLC is fully back in RUN state and ready for reads, including after network loss, program updates, and power cycles.
