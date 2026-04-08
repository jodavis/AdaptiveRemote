Feature: CompiledLayoutService Endpoints

Scenario: Get active compiled layout
    Given CompiledLayoutService is running
    When a test client calls GET /layouts/compiled/active
    Then the response is 200 OK
    And the body deserializes to a valid CompiledLayout using LayoutContractsJsonContext
    And the CompiledLayout contains the expected hardcoded commands
    And the service logs contain a request log entry for GET /layouts/compiled/active
    And the service logs contain no warnings or errors
