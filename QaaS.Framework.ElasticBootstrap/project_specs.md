# project_specs.md — QaaS.Framework.ElasticBootstrap

Helpers for Elastic configuration consumed by `QaaS.Framework.Executions`
when the Elasticsearch logging sink is enabled. Keeps the elastic-specific
plumbing isolated from the rest of the framework.

## Forbidden in this project

- Logging side effects beyond what `Executions` already wires.
- Adding queries / index management — that lives elsewhere
  (`QaaS.ElasticBootstrap` separate repo).
