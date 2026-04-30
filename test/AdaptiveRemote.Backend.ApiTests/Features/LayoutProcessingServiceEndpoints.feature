@ApiIntegrationTest
Feature: LayoutProcessingService Endpoints

Scenario: Health check returns 200 OK
    Given LayoutProcessingService is running
    When a test client calls GET /health
    Then the response is 200 OK
    And the body contains the LayoutProcessingService name and version
    And the service logs contain no warnings or errors

@PipelineTest
Scenario: End-to-end layout processing success path
    Given the layout processing pipeline is running
    When a raw layout is created via RawLayoutService
    Then the processing service logs show the layout was compiled and validated
    And the processing service logs show the compiled layout was stored
    And the processing service logs show no unhandled errors

@PipelineTest
Scenario: End-to-end layout processing validation failure path
    Given the layout processing pipeline is running with forced validation failure
    When a raw layout is created via RawLayoutService
    Then the processing service logs show the layout failed validation
    And the processing service logs show the validation result was written back
    And the processing service logs show no unhandled errors
