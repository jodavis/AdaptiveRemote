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
    And I should not see any warning or error messages in the LayoutProcessingService logs
    And I should not see any warning or error messages in the RawLayoutService logs

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
    Then I should see a message that contains "Layout compiled successfully" in the LayoutProcessingService logs
    And I should see a message that contains "Layout validation passed" in the LayoutProcessingService logs
    And I should see a message that contains "Compiled layout stored" in the LayoutProcessingService logs
    And I should see a message that contains "Layout-ready notification published" in the LayoutProcessingService logs
    And I should not see any warning or error messages in the LayoutProcessingService logs
    And I should not see any warning or error messages in the RawLayoutService logs

@PipelineTest
Scenario: End-to-end layout processing validation failure path
    Given LayoutProcessingService is running
    And the client has a valid Authorization token
    When this layout is created via RawLayoutService:
        # The element's cssId starts with "INVALID_", which causes LayoutCompilerService to
        # emit ".INVALID_up-btn { ... }" in CssDefinitions. StubLayoutValidationClient
        # detects that prefix and returns a failure result, exercising the failure path.
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
                    "cssId": "INVALID_up-btn",
                    "gridRow": 0,
                    "gridColumn": 0
                }
            ]
        }
        """
    Then I should see a message that contains "Layout compiled successfully" in the LayoutProcessingService logs
    And I should see a warning message in the LayoutProcessingService logs:
        """
        Layout validation failed
		"""
	And I should see a message that contains "Validation result written back to raw layout" in the LayoutProcessingService logs
    And I should not see any warning or error messages in the LayoutProcessingService logs
    And I should not see any warning or error messages in the RawLayoutService logs
