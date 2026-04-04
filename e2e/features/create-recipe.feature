Feature: Create Recipe

  Scenario: Creating a recipe navigates to its detail page
    Given I am on the recipe browser page
    When I create a recipe named "Grandma's Pierogi"
    Then I am on the detail page for "Grandma's Pierogi"
