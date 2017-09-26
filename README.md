# SelfHelpBot
I have taken the various Bot code examples that Geoff Innis created and merged and enhanced some of the functionality using the documentation on the Bot Developer Framework to create the SelfHelpBot.  

This allows you to do three things…

1)	Identify a Part of Product.  Take a picture of an object and the bot will run it through the custom vision service and if identified, it will send back details based on the Product in Dynamics 365. If a confidence level of above 75% is not achieved from the Vision Service, the user is informed that we cannot find a match…

2)	Knowledge Base Search.   Searches against the CRM KB and returns results that can be viewed on the customer portal.

3.  Create a Case.   This allows you to input your name and problem.  A CRM search will be done on the contact full name entered and if it finds a match, the case will be tied to the existing contact.  If the contact full name does not exist, a new contact will be created and tied to the case.

 
# Prerequisites for this...
-	A Dynamics 365 instance with the new Interactive Service Hub Knowledge Base setup
-	The same Dynamics 365 instance with the Customer Portal configured with Knowledge Base articles and cases exposed.
-	Dynamics 365 Products that have a product name that matches the tags used in the Custom Vision Service.  Also, a custom field to capture the product image URL.
-	A custom vision service project setup with multiple pictures per item created.  You can use the pictures in the .zip file attached.  For more information on using the custom vision service, see https://docs.microsoft.com/en-us/azure/cognitive-services/custom-vision-service/getting-started-build-a-classifier 
-	A comfort level with Visual Studio 2017.  You will need to modify the application settings in the Web Config file of the attached visual studio solution.  When finished you will need to publish it online so that is it registered to your Bot.
-	Download and install the Bot Framework Emulator at https://github.com/Microsoft/BotFramework-Emulator/releases/tag/v3.5.31
-	An existing Bot setup in the Bot Framework at https://dev.botframework.com/ 
-	A good understanding of Geoff Innis’ Product and Part Identifier Bot using Custom Vision Service blog posting located at https://blogs.msdn.microsoft.com/geoffreyinnis/2017/06/13/product-and-part-identifier-bot-using-custom-vision-service/ 
-	A good sense of humor and familiarity with Silicon Valley Season 4.

# To get this running, you will need to complete the following steps…
1)	Create a Bot on the Bot Framework and connect it to the Skype channel.
2)	Create a Custom Vision Service Project and upload your pictures and tag them appropriately.  Make sure to have some real life pictures of the items, not just pictures from the internet.  You can use the Pictures.zip file included in this project.
3)	Train the Custom Vision Service Project.  
4)	Download the Visual Studio Solution file and open in Visual Studio 2017 and modify the Application Settings under the web.config file so they you are using your Bot, your Custom Vision Service Project, your Dynamics 365 instance and your Dynamics 365 Customer Portal. 
5)	Create a custom field on the product entity called External Image URL.  It should be a Single Line of text / 300 field.
6)	Add the new External Image URL and URL fields to the Product Entity form.
7)	Add products to Dynamics 365 that match the products tagged in your Custom Vision Service project.  For example, I added the Arc Touch Mouse, the Sculpt Comfort Mouse and the Surface Pro.
8)	Add KB Articles to your Dynamics 365 instance using the new Interactive Service Hub Knowledge Base.
9)	Modify lines 155, 186 and 190 of the VisionDialog.cs file; replace cfs_externalimageurl with the schema name of your custom field on the product entity.
10)	Save your Visual Studio solution and test locally using the Bot Framework Emulator.
11)	Once satisfied with the results, publish it to Azure.  For a tutorial, go to https://docs.microsoft.com/en-us/bot-framework/deploy-bot-visual-studio. 
12)	Test your Bot in Skype.
NOTE:  For the case portion, make sure to use an existing contact that has access to the Dynamics 365 Customer Portal so you can see the case listing for that specific contact in the portal.

Note: This is not production level code.  This is for demo purposes only.

For more information on the functionality and setup, please watch the video at https://1drv.ms/v/s!AqPdeEsixg1ZkNtc1asY9PJixmJx9w and the instructions in word document format with screenshots are at https://1drv.ms/w/s!AqPdeEsixg1ZkNtdswlMBDQfMPuCjg. 
