Feature: Execute EolHosting Job 

Scenario: Mock source folder
	Given I have a mocked source folder returning 2 backup files
	And I create an instance of the EolHostingJob
	When I call the Execute method
	Then the result should be stored into the database
	And I should have 363 new rows in Account table
	And I should have 12 new raw tables
