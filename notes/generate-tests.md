I ran the below prompts in Claude Code to generate the verify.ps1 script while using a template.

```
I will use C:\repos\loop-agent\verify.ps1 as a test suite in a TDD methodology where the test need to start out all red.

Read the acceptance criteria from C:\repos\loop-agent\GOAL.md, read the verify.template.ps1 format, and update the verify.ps1 file that sends REST calls to test each of the AC.
```
```
Include a toggle that defaults to these debug messages being off. So that, the loop doesn't need to burn tokens but a user can turn it on.
```
```
Break out each acceptance test into it's own function and allow Run-AcceptanceTests to be a wrapper function for them.
```

The test command

```
pwsh -File verify.ps1 -Accept -ShowTests
```
