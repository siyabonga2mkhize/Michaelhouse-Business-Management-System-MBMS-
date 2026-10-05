## Cafeteria: dietary profiles, meal library, meal plans, kitchen plan, collection, events/RSVP and ingredient inventory

### What changed
- **Student dietary profile** (preference, allergies, medical restrictions) on `StudentProfile`, used by meal plans, menu coverage and the collection terminal.
- **Meal library (Dietitian):** create/edit meals from recipes; classification, halal status, allergens and nutrition are derived from the ingredients.
- **Menu scheduling** offers several options per slot so every dietary group has a choice, with coverage warnings. **Student meal plans** pick from the published menu: unsuitable meals are shown but disabled, all rules are re-checked on the server, and there is a day-before cutoff.
- **Kitchen plan** is built from submitted meal plans, with ingredient requirements and availability.
- **Meal collection:** face identification plus server-side checks (submitted plan, meal chosen, not already collected, on the menu) and a one-time confirmation token. The Cafeteria Manager can open collection manually for testing/demos.
- **Events / RSVP:**
  - RSVPs open on a schedule or manually.
  - Buffet meals come from the meal library, with a dietary coverage check.
  - Students, parents and staff RSVP with their login, choosing a meal (unsafe meals are blocked) and adding guests.
  - RSVPs close automatically and the plan is handed to the kitchen.
  - New "My Event Invitations" page.
- **Ingredient inventory:**
  - Suppliers reuse the Store's `Supplier` table, with a new `SuppliesCafeteria` flag.
  - Purchase orders: draft → sent → supplier confirmation → shortfall handling (accept, amend, or re-order from another supplier) → deliveries.
  - Invoice scanning uses Azure Document Intelligence (`prebuilt-invoice`); the manager confirms every line before stock changes.
  - Every stock change is an audited transaction.
  - The Chef issues ingredients per day (weekly menu) or per event.
  - Dashboard and mobile API.
- **Seed data:** 18 more meals, 20 ingredients, 8 demo suppliers and opening stock. It is idempotent and never overwrites edits or real stock.
- **Fixes:**
  - Event invitations and the daily menu now get a student's house from the active `ResidenceAllocation` (`StudentHouseService`); `Student.ResidenceId` is not filled in by the app.
  - The notification bell's script ran before jQuery had loaded.
  - The sidebar was unusable on phones; below 768 px it now slides over the page (desktop unchanged).

### For reviewers
- **Database:** schema changes need a local `Add-Migration` + `Update-Database` (the `Migrations/` folder isn't tracked, except `Configuration.cs`).
- **Not included:** `Web.config` (my local connection string) and `bin/` are deliberately left out of this PR.
- **Invoice scanning** needs the existing `AzureDocIntelligence:*` settings. Without them the delivery form falls back to manual entry. It hasn't been tested against the live Azure service yet.
- **Face matching** is still a simulation (image similarity, not biometric). Students enrolled under the old placeholder must re-enrol.
- **Existing seed quirk:** in `Configuration.Seed`, the original 30-meal seed runs inside the student loop (5× per update). Left as is; the new seed is called once, after the loop.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
