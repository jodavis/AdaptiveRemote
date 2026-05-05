Feature: Health Endpoints

Scenario: Get service health status
    Given CompiledLayoutService is running
    When a test client calls GET /health
    Then the response is 200 OK
    And the body contains the service name and version
