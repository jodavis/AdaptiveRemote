Feature: Health Endpoints

Scenario: Get service health status
    Given CompiledLayoutService is running
    When the client calls GET /health on the CompiledLayoutService endpoint
    Then the response is 200 OK
    And the response body is valid JSON
    And the response body represents a HealthResponse
	And the HealthResponse in the response body has "serviceName"="CompiledLayoutService"
	And the HealthResponse in the response body has "status"="healthy"
    And the HealthResponse in the response body has a "version" property
