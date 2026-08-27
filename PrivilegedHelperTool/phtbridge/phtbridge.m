#import "ServiceManagement/ServiceManagement.h"

#import "launcherProtocol.h"
#import "phtbridge.h"

typedef struct {
    NSXPCConnection *connection;
    id<launcherProtocol> remoteProxy;
    PHTErrorCallback errorCallback;
    BOOL isValid;
} XPCContext;

XPCContext *GetContext(void *context) {
    if (context == NULL) {
        return NULL;
    }

    XPCContext *xpcContext = (XPCContext *)context;
    if (!xpcContext->isValid) {
        if (xpcContext->errorCallback) {
            xpcContext->errorCallback(0, "Connection is not open");
        }
        return NULL;
    }

    return xpcContext;
}

void PHTRegisterService(PHTRegisterCallback callback) {
    SMAppService *service = [SMAppService
        daemonServiceWithPlistName:@"com.marmaladeengine.launcher.pht.plist"];

    NSError *error = nil;
    BOOL success = [service registerAndReturnError:&error];

    if (!success) {
        if (error.code == kSMErrorAlreadyRegistered) {
            if (callback) {
                callback(1, "The service has already been registered.");
            }
        } else if (error.code == kSMErrorLaunchDeniedByUser) {
            if (callback) {
                callback(2, "The service has been successfully registered, but "
                            "the user "
                            "needs to take action in System Preferences.");
            }
        } else {
            NSString *message =
                [NSString stringWithFormat:@"Failed to register service: %@",
                                           error.localizedDescription];
            if (callback) {
                callback(0, [message UTF8String]);
            }
        }
        return;
    }

    if (service.status == SMAppServiceStatusNotRegistered) {
        if (callback) {
            callback(
                0, "The service hasn’t registered with the Service Management "
                   "framework, or the service attempted to reregister after it "
                   "was already registered.");
        }
    } else if (service.status == SMAppServiceStatusEnabled) {
        if (callback) {
            callback(1, "The service has been successfully registered and is "
                        "eligible to run.");
        }
    } else if (service.status == SMAppServiceStatusNotFound) {
        if (callback) {
            callback(0,
                     "An error occurred and the framework couldn’t find this "
                     "service.");
        }
    }
}

void PHTUnregisterService(void) {
    SMAppService *service = [SMAppService
        daemonServiceWithPlistName:@"com.marmaladeengine.launcher.pht.plist"];
    [service unregisterAndReturnError:NULL];
}

void *PHTCreateConnection(PHTErrorCallback callback) {
    XPCContext *context = (XPCContext *)malloc(sizeof(XPCContext));
    context->isValid = YES;
    context->errorCallback = callback;

    NSXPCConnection *connection = [[NSXPCConnection alloc]
        initWithMachServiceName:@"com.marmaladeengine.launcher.pht"
                        options:NSXPCConnectionPrivileged];

    connection.remoteObjectInterface =
        [NSXPCInterface interfaceWithProtocol:@protocol(launcherProtocol)];

    connection.interruptionHandler = ^{
      if (context->errorCallback) {
          context->errorCallback(1, "Connection interrupted");
      }
    };

    connection.invalidationHandler = ^{
      if (context->errorCallback) {
          context->errorCallback(2, "Connection invalidated");
      }
    };

    [connection resume];

    id<launcherProtocol> service = [connection
        remoteObjectProxyWithErrorHandler:^(NSError *_Nonnull error) {
          NSString *message = [NSString
              stringWithFormat:@"Proxy error: %@", error.localizedDescription];
          if (context->errorCallback) {
              context->errorCallback(0, [message UTF8String]);
          }
        }];

    context->connection = connection;
    context->remoteProxy = service;

    return (void *)context;
}

void PHTCloseConnection(void *context) {
    XPCContext *xpcContext = GetContext(context);
    if (xpcContext == NULL)
        return;

    [xpcContext->connection invalidate];

    xpcContext->connection = nil;
    xpcContext->remoteProxy = nil;
    free(context);
}

void PHTGetUser(void *context, PHTGetUserCallback callback) {
    XPCContext *xpcContext = GetContext(context);
    if (xpcContext == NULL)
        return;

    [xpcContext->remoteProxy getUser:^(NSString *reply) {
      if (callback) {
          callback([reply UTF8String]);
      }
    }];
}
