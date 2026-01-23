//! File system watcher module
//!
//! Provides real-time file system change notifications using notify crate.

use notify::{Config, Event, RecommendedWatcher, RecursiveMode, Watcher};
use std::path::Path;
use std::sync::mpsc::{channel, Receiver};
use tracing::info;

/// File system change event
#[derive(Debug, Clone)]
pub struct WatchEvent {
    pub paths: Vec<String>,
    pub kind: WatchEventKind,
}

/// Types of watch events
#[derive(Debug, Clone)]
pub enum WatchEventKind {
    Create,
    Modify,
    Remove,
    Rename,
    Other,
}

impl From<&notify::EventKind> for WatchEventKind {
    fn from(kind: &notify::EventKind) -> Self {
        match kind {
            notify::EventKind::Create(_) => WatchEventKind::Create,
            notify::EventKind::Modify(_) => WatchEventKind::Modify,
            notify::EventKind::Remove(_) => WatchEventKind::Remove,
            _ => WatchEventKind::Other,
        }
    }
}

/// File system watcher
pub struct FileWatcher {
    _watcher: RecommendedWatcher,
}

impl FileWatcher {
    /// Create a new file watcher for the given path
    pub fn new<P: AsRef<Path>>(path: P) -> crate::Result<(Self, Receiver<WatchEvent>)> {
        let (tx, rx) = channel();

        let mut watcher = RecommendedWatcher::new(
            move |res: Result<Event, notify::Error>| {
                if let Ok(event) = res {
                    let watch_event = WatchEvent {
                        paths: event
                            .paths
                            .iter()
                            .map(|p| p.display().to_string())
                            .collect(),
                        kind: WatchEventKind::from(&event.kind),
                    };
                    let _ = tx.send(watch_event);
                }
            },
            Config::default(),
        )
        .map_err(|e| crate::NexusError::Io(std::io::Error::other(e)))?;

        watcher
            .watch(path.as_ref(), RecursiveMode::Recursive)
            .map_err(|e| crate::NexusError::Io(std::io::Error::other(e)))?;

        info!("File watcher started for: {}", path.as_ref().display());

        Ok((Self { _watcher: watcher }, rx))
    }
}
