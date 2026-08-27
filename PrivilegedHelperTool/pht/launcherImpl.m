#import "launcherImpl.h"

@implementation launcherImpl

- (void)getUser:(void (^)(NSString *))reply {
    NSString *user = NSUserName();
    reply(user);
}

@end
