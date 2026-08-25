#import <Foundation/Foundation.h>
#import <xpc/xpc.h>

int main(int argc, const char * argv[]) {
    @autoreleasepool {
        xpc_connection_t listener = xpc_connection_create_mach_service("com.marmaladeengine.launcher.pht", NULL, XPC_CONNECTION_MACH_SERVICE_LISTENER);
        
        xpc_connection_set_event_handler(listener, ^(xpc_object_t peer) {
            if (xpc_get_type(peer) != XPC_TYPE_CONNECTION) {
                return;
            }
            
            xpc_connection_set_event_handler(peer, ^(xpc_object_t request) {
                if (xpc_get_type(request) == XPC_TYPE_DICTIONARY) {
                    
                    const char *messageCString = xpc_dictionary_get_string(request, "MessageKey");
                    NSString *encodedMessage = messageCString ? [NSString stringWithUTF8String:messageCString] : @"";
                    
                    xpc_object_t reply = xpc_dictionary_create_reply(request);
                    if (reply) {
                        xpc_dictionary_set_string(reply, "ResponseKey", "Test");
                        xpc_connection_send_message(peer, reply);
                    }
                }
            });
            
            xpc_connection_activate(peer);
        });
        
        xpc_connection_activate(listener);
        
        dispatch_main();
    }
    return 0;
}
