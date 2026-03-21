# Release Checklist

Updated: 2026-03-20

## Launch

- verify Windows launcher starts the packaged app with no console window
- verify macOS packaged app launches cleanly
- verify the single-instance guard prevents duplicate windows

## Hidden Workflow Processes

- verify hidden workflow actions run with no visible terminal window
- verify hidden workflow actions stop automatically when the app closes
- verify `Stop All Automation` pauses visible AI/manual queue state cleanly

## Project Structure

- verify expected project-local outputs stay under `.sprite-workflow/`
- verify review/request/candidate stores round-trip without manual migration
- verify starter workspaces create authored/runtime/incoming/artifact roots correctly

## Validation And Readiness

- run validation and confirm markdown/json reports are produced
- verify project readiness reflects blockers, approval state, validation state, and export readiness
- verify trusted export blockers jump back to the correct row/frame

## Export And Portability

- export a trusted set
- export a project kit
- create a validation sandbox
- open each output from inside the app and confirm paths are project-local and inspectable

## Cross-Project Checks

- load the Wevito profile with no manual conversion
- load the blank starter sample profile
- load the imported external sample profile
- confirm the app does not require Wevito-specific paths or folder names to function
