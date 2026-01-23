OpenSAMLSP Setup and Tutorial

Prerequisites:
Java 1.8.0 installed.
Maven 3.0.4+ installed.

Verify if Java and Maven are installed properly by opening a CLI and running these commands : 
java -version 
mvn -version

Portecle app downloaded from http://sourceforge.net/projects/portecle/ to edit Java keystore files
The download is a zip file, you can just extract all files and copy the resulting folder to a location of your choice.

1. 	Get the code for the test app:
	The PowerSchool district administrator will provide you with an archive containing the source code in maven format. Follow the instructions below to get the sample app up and running 
	using PowerSchool as the federated IdP for your service provider. 
   
2. 	Extract the contents of the OpenSamlSP archive into your desired workspace.
   
3.      Install Plugin: In PowerSchool, System-->System Settings-->Plugin Management-->Import/Install then select the test plugin file C:\path\to\OpenSamlSP\spring-security-saml-plugin.xml
   
4.	Navigate to Single Sign-on Setup page then right click on view PowerSchool IDP Metadata and select Save Link As option.	Copy the contents from downloaded IDP Metadata xml. Paste metadata xml
	to C:\path\to\OpenSamlSP\saml2-sample\src\main\resources\security\idp.xml and save it.	

4. 	Set local environment variables: CATALINA_HOME to the included tomcat container using the path where your OpenSamlSP folder is located and JAVA_HOME to the Java8 JDK. Examples are below:
        set CATALINA_HOME=C:\path\to\OpenSamlSP\apache-tomcat-8
        set JAVA_HOME=C:\Program Files\Java\jdk1.8.0_51
	 
7. 	From a CLI: cd C:\path\to\OpenSamlSP\

8.      Enter: run_app 
	(This will use maven and build the app, then run the app. Subsequent executions will not rebuild the app, but will simply run the app, unless you use 
	the clean_app script to clean it. If you make any changes to securityContext.xml then run clean_app script prior to running the app. The app will run in the forground of the CLI. Cmd-C to exit.)
   
9.      Confirm the app runs without major errors.

10.     Test Single Sign On: click the "plug" icon in the breadcrumbs banner and click the "SpringSAML Demo" link, or click the left navBar "SpringSAML Demo" link.
	SSO should succeed with a diplay showing SAML XML response with attributes passed. Check attributes are correct for current PowerSchool user.
	To repeat process click the Logout link at the bottom of the page.

The source code uses Spring Security and OpenSaml for Java packages. You can customize many of the settings by editing the securityContext.xml which acts as the application context file. That said, the example is really only 
intended to illustrate the specific requirements for federating with PowerSchool as a SAML IdP. 

