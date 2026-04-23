---
status: draft
---

# Design Specification: Miscellaneous improvements

## restyle fake idp screens (more minimal)

- simplify the UI to focus on essential elements
- use a clean and modern design aesthetic
- remove unnecessary graphics and distractions
- ensure that the screens are responsive and work well on different devices

## Identity Management based on ASP.NET Identity

https://duendesoftware.com/learn/role-based-access-control-asp-net-core-identity

https://dev.to/tural_hasanov_11/permission-based-authentication-and-authorization-in-net-via-cookies-47fh

- manage user/roles/permissions
- based on ASP.NET Identity for robust and secure identity management
- dbcontext backed for persistence
- provide an interface/endpoints for managing users, roles, and permissions (e.g. create, update, delete)
- does not replace authentication, but provides a way to manage identities and their associated roles and permissions in the application by leveraging ASP.NET Identity for secure and scalable identity management
- consider integration with external identity providers (e.g. OAuth, OpenID Connect) for authentication while still using ASP.NET Identity for managing user information and roles within the application
- ensure that the identity management system is flexible and extensible to accommodate future requirements (e.g. additional user attributes, custom role hierarchies)
- implement appropriate security measures to protect user data and prevent unauthorized access to identity management features (e.g. role-based access control, input validation, secure password storage)
- provide documentation on how to use the identity management features, including how to create and manage users, roles, and permissions, as well as best practices for securing the identity management system
- does not manage passwords
- standard permissions (can edit Customers), not resource based permissions (can edit Customer #xyz")
- usege of the Authorize attribute on Commands/Queries (Handle) too  to enforce permissions instead of only on Controller/Minimal API endpoints
- permissions can be granted to users or roles
- enfore with standard asp.net authorization policies (e.g. [Authorize(Policy = "CanEditCustomers")])
- permissions can be disabled for specific users or roles
- permissions dont have child permissions (e.g. "CanEditCustomers" does not automatically grant "CanViewCustomers")
-

## Audit Feature

- log all relevant events (e.g. login attempts, password changes, role assignments)
- store logs in a secure and queryable format (e.g. database, log management system)
- provide an interface for querying and analyzing audit logs (e.g. filtering by user, event type, date range)
- ensure compliance with relevant regulations (e.g. GDPR, HIPAA) regarding data retention and access to audit logs
- implement access controls for audit logs to prevent unauthorized access
- consider performance implications of logging and ensure that it does not significantly impact application performance
- provide documentation on how to use the audit feature, including how to query logs and interpret the data
- consider integration with external monitoring and alerting systems to notify administrators of suspicious activity detected in audit logs
- implement a retention policy for audit logs to manage storage and ensure that old logs are archived or deleted according to organizational policies
- consider implementing a tamper-evident logging mechanism to ensure the integrity of audit logs and prevent unauthorized modifications

## Feature Management Feature

- define features
- feature toggles
- apply to code blocks
- appsettings config + persistent overrides (dbcontext)
- DoFiesta: UI for managing features and overrides (based on REST endpoints)
- Feature-based authorization (e.g. only show certain UI elements if a feature is enabled)
- Testing: unit tests for feature management logic, integration tests for feature toggles in action
- Documentation: how to define and use features, best practices for feature management
- Rollout strategies: gradual rollout, percentage-based rollout, user targeting
- Metrics and monitoring: track feature usage and impact, monitor for issues after rollout
- Security considerations: ensure that feature toggles cannot be exploited for unauthorized access or functionality
- Performance considerations: minimize overhead of feature checks in code, especially in high-traffic areas
