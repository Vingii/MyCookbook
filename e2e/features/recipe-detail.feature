Feature: Recipe Detail Page

  Scenario: Clicking a recipe opens its detail page
    Given a recipe named "Shakshuka" exists
    And I am on the recipe browser page
    When I click on "Shakshuka" in the recipe list
    Then I am on the detail page for "Shakshuka"

  Scenario: Deleting a recipe returns to the browser
    Given a recipe named "Delete Me Please" exists
    And I am on the recipe browser page
    When I click on "Delete Me Please" in the recipe list
    And I delete the current recipe
    Then I am on the recipe browser page
    And I do not see "Delete Me Please" in the recipe list
