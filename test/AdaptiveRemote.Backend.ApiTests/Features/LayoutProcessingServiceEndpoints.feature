@ApiIntegrationTest
Feature: LayoutProcessingService Endpoints

Scenario: Health check returns 200 OK
    Given LayoutProcessingService is running
    And the client has no Authorization token
	When the client calls GET /health on the LayoutProcessingService endpoint
    Then the response is 200 OK
    And the response body is valid JSON
    And the response body represents a HealthResponse
	And the HealthResponse in the response body has "serviceName"="LayoutProcessingService"
	And the HealthResponse in the response body has "status"="Healthy"
    And the HealthResponse in the response body has a "version" property
    And the RawLayoutService logs contain no warnings or errors

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
