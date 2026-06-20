# Risk Levels

Every tool call must record:

- mission id
- actor
- tool
- risk level
- input summary
- output summary
- result
- timestamp
- correlation id

Repeated failures at the same level should trigger the watchdog.

