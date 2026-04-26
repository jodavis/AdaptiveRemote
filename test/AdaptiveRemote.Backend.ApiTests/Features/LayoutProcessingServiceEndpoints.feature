@ApiIntegrationTest
Feature: LayoutProcessingService Endpoints

Scenario: Health check returns 200 OK
    Given LayoutProcessingService is running
    When a test client calls GET /health
    Then the response is 200 OK
    And the body contains the LayoutProcessingService name and version
    And the service logs contain no warnings or errors
