Feature: CompiledLayoutService Authentication

Scenario: Unauthenticated request is rejected
    Given CompiledLayoutService is running
    When a test client with no Authorization header calls GET /layouts/compiled/active
    Then the response is 401 Unauthorized

Scenario: Request with valid JWT is accepted
    Given CompiledLayoutService is running
    When a test client with a valid JWT calls GET /layouts/compiled/active
    Then the response is 200 OK

Scenario: Request with expired JWT is rejected
    Given CompiledLayoutService is running
    When a test client with an expired JWT calls GET /layouts/compiled/active
    Then the response is 401 Unauthorized

Scenario: Health endpoint is accessible without authentication
    Given CompiledLayoutService is running
    When a test client with no Authorization header calls GET /health
    Then the response is 200 OK
