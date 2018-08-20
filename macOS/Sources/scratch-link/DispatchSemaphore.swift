import Foundation

extension DispatchSemaphore {
    // Treat this semaphore as a mutex to wrap the given task. Specifically:
    // 1. Call `wait()` to acquire/lock the mutex
    // 2. Run the task
    // 3. Call `signal()` to release/unlock the mutex
    // Note that step 3 will run even if step 2 throws an exception.
    // If the task does not throw then this method will return the task's return value.
    func mutex<T>(_ task: () throws -> T) rethrows -> T {
        self.wait()
        defer { self.signal() }
        return try task()
    }
}
