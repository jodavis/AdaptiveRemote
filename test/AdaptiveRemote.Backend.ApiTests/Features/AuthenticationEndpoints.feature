Feature: CompiledLayoutService Authentication

Scenario: Unauthenticated request is rejected
    Given CompiledLayoutService is running
    And the client has no Authorization token
    When the client calls GET /layouts/compiled/active on the CompiledLayoutService endpoint
	Then the response is 401 Unauthorized
    And I should not see any warning or error messages in the CompiledLayoutService logs

Scenario: Request with valid JWT is accepted
    Given CompiledLayoutService is running
    And the client has a valid Authorization token
    When the client calls GET /layouts/compiled/active on the CompiledLayoutService endpoint
    Then the response is 200 OK
    And I should not see any warning or error messages in the CompiledLayoutService logs

Scenario: Request with expired JWT is rejected
    Given CompiledLayoutService is running
    And the client has an expired Authorization token
    When the client calls GET /layouts/compiled/active on the CompiledLayoutService endpoint
    Then the response is 401 Unauthorized
    And I should not see any warning or error messages in the CompiledLayoutService logs

Scenario: Health endpoint is accessible without authentication
    Given CompiledLayoutService is running
    And the client has no Authorization token
    When the client calls GET /health on the CompiledLayoutService endpoint
	Then the response is 200 OK
    And I should not see any warning or error messages in the CompiledLayoutService logs
