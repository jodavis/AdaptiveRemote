@ApiIntegrationTest
Feature: LayoutCompilerService Endpoints

Scenario: Compile a layout with a single command element
    Given LayoutCompilerService is running
    And the client has no Authorization token
    And a RawLayout with a command element at grid position (1, 1) is stored
    When the test client calls POST /compile on the LayoutCompilerService endpoint with the stored RawLayout
    Then the response is 200 OK
    And the response body is valid JSON
    And the response body represents a CompiledLayout
    And the CompiledLayout contains a CommandDefinitionDto with name "Up"
    And the CompiledLayout CSS definitions contain a rule for grid position (1, 1)
    And the CompiledLayout does not contain authoring properties on its elements
    And I should not see any warning or error messages in the LayoutCompilerService logs

Scenario: Compile a preview layout
    Given LayoutCompilerService is running
    And the client has no Authorization token
    And a list of RawLayoutElementDto with a command element at grid position (1, 1) is stored
    When the test client calls POST /compile/preview on the LayoutCompilerService endpoint with the stored elements
    Then the response is 200 OK
    And the response body is valid JSON
    And the response body represents a PreviewLayout
    And the PreviewLayout RenderedHtml is non-empty
    And the PreviewLayout RenderedCss is non-empty
    And I should not see any warning or error messages in the LayoutCompilerService logs
