#import <Foundation/Foundation.h>
#import <xpc/xpc.h>

#import "launcherImpl.h"

@interface ServiceDelegate : NSObject <NSXPCListenerDelegate>
@end

@implementation ServiceDelegate

- (BOOL)listener:(NSXPCListener *)listener
    shouldAcceptNewConnection:(NSXPCConnection *)newConnection {

    newConnection.exportedInterface =
        [NSXPCInterface interfaceWithProtocol:@protocol(launcherProtocol)];
    [newConnection
        setCodeSigningRequirement:
            @"anchor apple generic and certificate leaf[subject.CN] = "
            @"\"Developer ID Application: Grey Cat Games Ltd. (7K2D6HGD7U)\""];

    launcherImpl *exportedObject = [launcherImpl new];
    newConnection.exportedObject = exportedObject;

    [newConnection resume];

    return YES;
}

@end

int main(int argc, const char *argv[]) {

    ServiceDelegate *delegate = [ServiceDelegate new];

    NSXPCListener *listener = [[NSXPCListener alloc]
        initWithMachServiceName:@"com.marmaladeengine.launcher.pht"];
    listener.delegate = delegate;

    [listener resume];

    [[NSRunLoop currentRunLoop] run];

    return 0;
}
