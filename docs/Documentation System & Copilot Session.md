# Documentation System & Copilot Sessions

## Overview

This `docs/` folder serves as **local memory** for GitHub Copilot to understand the thesis project, its context, and ongoing work. It helps maintain continuity across chat sessions and prevents redundant explanations.

## Folder Structure

```
docs/
├── README.md (this file)
└── copilot_sessions/
    ├── 2026-02-24_thesis_comprehensive_summary.md
    ├── 2026-02-24_git_cleanup_and_gitattributes.md
    ├── YYYY-MM-DD_session_topic.md
    └── ...
```

## How It Works

### 1. **Session Naming Convention**
Each chat session creates a new markdown file with the format:
```
YYYY-MM-DD_brief_topic_description.md
```

**Examples**:
- `2026-02-24_git_cleanup_and_gitattributes.md` → Git workflow fixes
- `2026-03-01_unity_terrain_import.md` → Unity terrain setup
- `2026-03-15_lidar_processing_pipeline.md` → LiDAR data handling

### 2. **Session File Contents**
Each file documents:
- **Date & Topic**: What work was done
- **Problem**: What issue was being solved
- **Solution**: Code, commands, or approach used
- **Outcome**: What was accomplished
- **Next Steps**: What comes after

**Template**:
```markdown
# Session: [Date] - [Topic]

## Problem
[What needed to be solved]

## Solution
[Code/approach/explanation]

## Outcome
[What was completed]

## Next Steps
[What's coming next]

## Links/References
[Related files, commands, or previous sessions]
```

### 3. **Copilot Workflow**
At the start of each session:
1. User attaches relevant files from `docs/` folder
2. Copilot reads session history to understand context
3. Copilot provides targeted help aligned with thesis goals
4. At session end, user (or Copilot) summarizes key points

### 4. **When to Create a New File**
Create a new session file when:
- Starting a new major feature or phase
- Switching between different technical areas (GIS → Unity → Interaction Design)
- Significant time gap between chats (>1 day)
- Completing a milestone

### 5. **Updates & Maintenance**
- Keep files **concise but complete**
- Link between related sessions using markdown links: `[See git cleanup session](2026-02-24_git_cleanup_and_gitattributes.md)`
- Archive very old sessions if the repository becomes too large
- Only attach relevant session files to new chats (saves token budget)

## Current Project State

**Thesis**: Immersive Analytics in Urban Digital Twin  
**Student**: Iman Alavi Zadeh (Chalmers University)  
**Timeline**: ~6 months (Feb–Aug 2026)  
**Location**: Gothenburg, Sweden prototype  

### Key Completed Tasks
- ✅ Git repository cleanup (removed Raw_data from history)
- ✅ `.gitattributes` configuration for GIS data
- ✅ `.gitignore` setup to exclude raw data

### Upcoming Phases
1. GIS data processing pipeline (Python/GDAL)
2. Unity integration (terrain, buildings, LOD)
3. Temporal immersive interaction design
4. User evaluation study

## Quick Links

- **Thesis Summary**: [2026-02-24_thesis_comprehensive_summary.md](2026-02-24_thesis_comprehensive_summary.md)
- **Git & Setup**: [2026-02-24_git_cleanup_and_gitattributes.md](2026-02-24_git_cleanup_and_gitattributes.md)
- **Raw Data Location**: `w:\Investigating Usability of Immersive Analytics in an Urban Digital Twin\Portotype\Raw_data\` (git-ignored)

## Questions?

When starting a new Copilot chat:
1. Attach the main thesis summary file
2. Attach any relevant session files from this folder
3. Ask your question with full context

Copilot will use this documentation to understand your project and provide better, more targeted assistance.