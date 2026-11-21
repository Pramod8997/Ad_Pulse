# AdPulse — Change-Only Evolution Pack

This pack corrects the implementation strategy for the already completed AdPulse product.

**This is not a rebuild.** The existing repository, working functionality, stack, database, APIs, frontend and deployment are the baseline. The goal is to evolve the product toward an Advertising Operations & Intelligence Platform through small, backward-compatible changes.

Read `docs/01-change-only-strategy.md` first, then use the phase prompts.

## Core rule

Do not create a second AdPulse. Do not generate a fresh application, replace the existing architecture, rewrite working modules or discard existing features. Inspect the current repository and build an evidence-based change plan before modifying code.

## Suggested order

1. Commit or otherwise preserve a clean recovery point.
2. Run the current build and tests; record baseline results.
3. Complete the repository and feature inventory.
4. Map existing features to the new product direction using Keep / Modify / Extend / Add / Retire.
5. Get the change plan reviewed.
6. Phase 1 (GPT-6 Astra): difficult, high-risk changes to the existing implementation.
7. Phase 2 (GPT-6.1 Sol): UI/workflow adaptation, integration polish, documentation and cleanup.
8. Verify that previously working behavior still works.

The two model labels are execution assignments requested by the project owner; actual model availability and routing must be checked in the coding environment.
