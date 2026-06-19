// Localization tests mutate process-wide state: Loc.Initialize sets the active language
// and CultureInfo.CurrentCulture. Other tests assume Loc's uninitialized Korean default,
// so the whole assembly runs serially to keep those two from racing. The suite is small
// and I/O-light, so serial execution costs little.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
