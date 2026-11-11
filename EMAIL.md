# Email delivery

This API renders the existing database email templates and publishes requests to
Kafka. The Rust [Exatech email sender](https://github.com/KillerMaker/exatech_email_sender)
consumes those requests and handles SMTP delivery.

Configure the API using `appsettings.json` or environment variables:

| Environment variable | Default | Purpose |
| --- | --- | --- |
| `EmailQueue__BootstrapServers` | `localhost:9092` | Kafka bootstrap servers reachable from the API |
| `EmailQueue__Topic` | `email.requests` | Must match the Rust service's `KAFKA_TOPIC` |
| `EmailQueue__System` | `exatech-indotel-api` | Source system included in each request |

Keep `EmailReciver` (existing spelling), `ClientCreationTemplate`,
`ClientUpdateRequestTemplate`, and `UserCreationTemplate` configured as before.
SMTP credentials belong only to the Rust service. This API no longer requires
the old SMTP, Event Hubs, or Blob checkpoint settings.

For local development, start the Rust repository's Docker Compose stack. An API
running on the host uses `localhost:9092`; an API on the same Compose network
uses `kafka:19092`. For deployment, override the bootstrap servers with broker
addresses reachable from the API; `localhost` refers to the API's own host.

Requests use this format (including an empty array when there are no copies):

```json
{"to":"recipient@example.com","subject":"Subject","body":"<p>HTML content</p>","cc":[],"system":"exatech-indotel-api"}
```

Email operations await Kafka acknowledgment and propagate publishing failures.
Acknowledgment means the request was queued, not that SMTP delivery completed.
The producer enables idempotence for its own retries; this does not prevent
duplicate emails from repeated API requests or consumer redelivery.

To verify delivery, run both services, trigger an existing client/user email
workflow using a test recipient, and check the Rust service logs and mailbox.
This sends a real email.
