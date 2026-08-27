#import <Foundation/Foundation.h>

@protocol launcherProtocol

- (void)getUser:(void (^)(NSString *))reply;

@end
