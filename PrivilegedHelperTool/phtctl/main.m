#import <Foundation/Foundation.h>
#import <ServiceManagement/ServiceManagement.h>

#import "launcherProtocol.h"

int main(int argc, const char *argv[]) {
    if (argc < 2) {
        NSLog(@"Arguments: [register, unregister, test]");
        return EXIT_SUCCESS;
    }

    if (strcmp(argv[1], "register") == 0) {
        NSLog(@"Installing helper");

        SMAppService *service =
            [SMAppService daemonServiceWithPlistName:
                              @"com.marmaladeengine.launcher.pht.plist"];
        NSError *error = nil;
        BOOL success = [service registerAndReturnError:&error];

        if (!success) {
            NSLog(@"Failed to register service: %@",
                  error.localizedDescription);
        }

        if (service.status == SMAppServiceStatusNotRegistered) {
            NSLog(@"The service hasn’t registered with the Service Management "
                  @"framework, or the service attempted to reregister after it "
                  @"was already registered.");
        } else if (service.status == SMAppServiceStatusEnabled) {
            NSLog(@"The service has been successfully registered and is "
                  @"eligible to run.");
        } else if (service.status == SMAppServiceStatusRequiresApproval) {
            NSLog(@"The service has been successfully registered, but the user "
                  @"needs to take action in System Preferences.");
        } else if (service.status == SMAppServiceStatusNotFound) {
            NSLog(@"An error occurred and the framework couldn’t find this "
                  @"service.");
        }

        return EXIT_SUCCESS;
    } else if (strcmp(argv[1], "unregister") == 0) {
        NSLog(@"Unregistering helper");

        SMAppService *service =
            [SMAppService daemonServiceWithPlistName:
                              @"com.marmaladeengine.launcher.pht.plist"];
        [service unregisterAndReturnError:NULL];

        return EXIT_SUCCESS;
    } else if (strcmp(argv[1], "test") == 0) {
        NSLog(@"Testing XPC Connection");

        NSXPCConnection *connection = [[NSXPCConnection alloc]
            initWithMachServiceName:@"com.marmaladeengine.launcher.pht"
                            options:NSXPCConnectionPrivileged];

        connection.remoteObjectInterface =
            [NSXPCInterface interfaceWithProtocol:@protocol(launcherProtocol)];

        connection.interruptionHandler = ^{
          NSLog(@"Connection interrupted!");
        };

        connection.invalidationHandler = ^{
          NSLog(@"Connection invalidated!");
        };

        [connection resume];

        id<launcherProtocol> service = [connection
            remoteObjectProxyWithErrorHandler:^(NSError *_Nonnull error) {
              NSLog(@"Proxy error: %@", error.localizedDescription);
              exit(EXIT_SUCCESS);
            }];

        [service getUser:^(NSString *_Nonnull response) {
                                      NSLog(@"Received response: %@", response);
                                      exit(EXIT_FAILURE);
                                    }];

        [[NSRunLoop currentRunLoop] run];

        return EXIT_SUCCESS;
    } else {
        NSLog(@"Invalid argument");
        return EXIT_SUCCESS;
    }
}
