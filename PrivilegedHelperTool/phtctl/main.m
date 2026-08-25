#import <Foundation/Foundation.h>
#import <ServiceManagement/ServiceManagement.h>

int main(int argc, const char * argv[]) {
    if (argc < 2) {
        NSLog(@"Arguments: [register, unregister, test]");
        return EXIT_SUCCESS;
    }
    
    if (strcmp(argv[1], "register") == 0) {
        NSLog(@"Installing helper");
        
        SMAppService *service = [SMAppService daemonServiceWithPlistName:@"com.marmaladeengine.launcher.pht.plist"];
        NSError *error = nil;
        BOOL success = [service registerAndReturnError:&error];
        
        if (!success) {
            NSLog(@"Failed to register service: %@", error.localizedDescription);
        }
        
        if(service.status == SMAppServiceStatusNotRegistered) {
            NSLog(@"The service hasn’t registered with the Service Management framework, or the service attempted to reregister after it was already registered.");
        }
        else if (service.status == SMAppServiceStatusEnabled) {
            NSLog(@"The service has been successfully registered and is eligible to run.");
        }
        else if(service.status == SMAppServiceStatusRequiresApproval) {
            NSLog(@"The service has been successfully registered, but the user needs to take action in System Preferences.");
        }
        else if(service.status == SMAppServiceStatusNotFound) {
            NSLog(@"An error occurred and the framework couldn’t find this service.");
        }
        
        return EXIT_SUCCESS;
    } else if (strcmp(argv[1], "unregister") == 0) {
        NSLog(@"Unregistering helper");
        
        SMAppService *service = [SMAppService daemonServiceWithPlistName:@"com.marmaladeengine.launcher.pht.plist"];
        [service unregisterAndReturnError:NULL];
        
        return EXIT_SUCCESS;
    } else if (strcmp(argv[1], "test") == 0) {
        NSLog(@"Testing XPC Connection");
        
        NSString *messageText = @"Hello";
        
        xpc_object_t request = xpc_dictionary_create_empty();
        xpc_dictionary_set_string(request, "MessageKey", [messageText UTF8String]);
        
        xpc_rich_error_t error = NULL;
        xpc_session_t session = xpc_session_create_mach_service(
                                                                "com.marmaladeengine.launcher.pht",
                                                                NULL,
                                                                XPC_SESSION_CREATE_NONE,
                                                                &error
                                                                );
        
        if (error != NULL) {
            char *errorDescription = xpc_rich_error_copy_description(error);
            NSLog(@"Unable to create xpc_session: %s", errorDescription);
            free(errorDescription);
            exit(1);
        }
        
        xpc_object_t reply = xpc_session_send_message_with_reply_sync(session, request, &error);
        
        if (error != NULL) {
            char *errorDescription = xpc_rich_error_copy_description(error);
            NSLog(@"Error sending message: %s", errorDescription);
            free(errorDescription);
            exit(1);
        }
        
        if (reply != NULL && xpc_get_type(reply) == XPC_TYPE_DICTIONARY) {
            const char *responseCString = xpc_dictionary_get_string(reply, "ResponseKey");
            if (responseCString != NULL) {
                NSString *encodedResponse = [NSString stringWithUTF8String:responseCString];
                NSLog(@"Received \"%@\"", encodedResponse);
            }
        }
        
        xpc_session_cancel(session);
        
        return EXIT_SUCCESS;
    } else {
        NSLog(@"Invalid argument");
        return EXIT_SUCCESS;
    }
}
