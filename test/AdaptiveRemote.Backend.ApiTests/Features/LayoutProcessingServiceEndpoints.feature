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
    Given LayoutProcessingService is running
    And the client has a valid Authorization token
    When this layout is created via RawLayoutService:
        """
        {
            "userId": "test-user",
            "name": "Pipeline Test Layout",
            "elements": [
                {
                    "$type": "command",
                    "type": 1,
                    "name": "Up",
                    "label": "Up",
                    "speakPhrase": "up",
                    "reverse": "Down",
                    "cssId": "up-btn",
                    "gridRow": 0,
                    "gridColumn": 0
                }
            ]
        }
        """
    Then the LayoutProcessingService logs contain the message "Layout compiled successfully"
    And the LayoutProcessingService logs contain the message "Layout validation passed"
    And the LayoutProcessingService logs contain the message "Compiled layout stored"
    And the LayoutProcessingService logs contain the message "Layout-ready notification published"
    And the LayoutProcessingService logs contain no warnings or errors

@PipelineTest
Scenario: End-to-end layout processing validation failure path
    Given LayoutProcessingService is running
    And the client has a valid Authorization token
    When this layout is created via RawLayoutService:
        # Invalid because it has a special "name" that is considered invalid
        # for testing purposes
        """
        {
            "userId": "test-user",
            "name": "Invalid Pipeline Test Layout",
            "elements": [
                {
                    "$type": "command",
                    "type": 1,
                    "name": "Up",
                    "label": "Up",
                    "speakPhrase": "up",
                    "reverse": "Down",
                    "cssId": "up-btn",
                    "gridRow": 0,
                    "gridColumn": 0
                }
            ]
        }
        """
    Then the LayoutProcessingService logs contain the message "Layout compiled successfully"
    And the LayoutProcessingService logs contain the message "Layout validation failed"
    And the LayoutProcessingService logs contain the message "Validation result written back to raw layout"
    And the LayoutProcessingService logs contain no warnings or errors
