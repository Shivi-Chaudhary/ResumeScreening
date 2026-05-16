import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe, DecimalPipe } from '@angular/common';
import {
  JobsService,
  JobDetail,
  ResumeListItem,
  BulkResumeUploadResponse,
  RankedCandidate,
  ResumeDetail,
} from '../../core/jobs.service';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-job-detail',
  standalone: true,
  imports: [RouterLink, FormsModule, ReactiveFormsModule, DatePipe, DecimalPipe],
  templateUrl: './job-detail.component.html',
  styleUrl: './job-detail.component.scss',
})
export class JobDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);
  private readonly jobsApi = inject(JobsService);
  readonly auth = inject(AuthService);

  readonly job = signal<JobDetail | null>(null);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly saveMsg = signal('');
  readonly jdMsg = signal('');

  jobId = 0;

  readonly editForm = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    description: ['', [Validators.required]],
    status: ['Active' as 'Active' | 'Closed'],
  });

  saving = false;
  jdUploading = false;
  deleting = false;

  readonly resumes = signal<ResumeListItem[]>([]);
  readonly resumesLoading = signal(false);
  readonly resumeUploading = signal(false);
  readonly myResumeBusy = signal(false);
  readonly resumeMsg = signal('');
  readonly resumePickHint = signal('');
  /** Resume row id being deleted (disables that row's button) */
  readonly deletingResumeId = signal<number | null>(null);

  /** Queued PDFs before upload (max 20) */
  selectedResumeFiles: File[] = [];

  // ── AI Screening ───────────────────────────────────────────────────────
  readonly screening = signal(false);
  readonly screeningMsg = signal('');
  readonly screeningProgress = signal(0);
  private screeningInterval: ReturnType<typeof setInterval> | null = null;

  readonly rankings = signal<RankedCandidate[]>([]);
  readonly rankingsLoading = signal(false);

  // ── Resume Detail Modal ────────────────────────────────────────────────
  readonly selectedResume = signal<ResumeDetail | null>(null);
  readonly resumeDetailLoading = signal(false);
  readonly showResumeModal = signal(false);

  // ── HR Status ──────────────────────────────────────────────────────────
  hrStatusValue = 'Pending';
  hrNotesValue = '';
  readonly hrStatusSaving = signal(false);
  readonly hrStatusMsg = signal('');

  // ── Export ─────────────────────────────────────────────────────────────
  readonly exporting = signal(false);

  ngOnInit(): void {
    this.route.paramMap.subscribe((params) => {
      const id = Number(params.get('id'));
      if (!Number.isFinite(id)) {
        void this.router.navigate(['/jobs']);
        return;
      }
      this.jobId = id;
      this.load();
    });
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.resumeMsg.set('');
    this.jobsApi.get(this.jobId).subscribe({
      next: (j) => {
        this.job.set(j);
        this.editForm.patchValue({
          title: j.title,
          description: j.description,
          status: j.status === 'Closed' ? 'Closed' : 'Active',
        });
        this.loading.set(false);
        this.loadResumes();
        this.loadRankings();
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err?.error?.message ?? 'Job not found.');
      },
    });
  }

  loadResumes(): void {
    this.resumesLoading.set(true);
    this.jobsApi.listResumes(this.jobId).subscribe({
      next: (rows) => {
        this.resumes.set(rows);
        this.resumesLoading.set(false);
      },
      error: () => {
        this.resumes.set([]);
        this.resumesLoading.set(false);
      },
    });
  }

  loadRankings(): void {
    this.rankingsLoading.set(true);
    this.jobsApi.getRankings(this.jobId).subscribe({
      next: (rows) => {
        this.rankings.set(rows);
        this.rankingsLoading.set(false);
      },
      error: () => {
        this.rankings.set([]);
        this.rankingsLoading.set(false);
      },
    });
  }

  // ── AI Screening ───────────────────────────────────────────────────────
  runScreening(): void {
    if (!this.auth.isHrAdmin()) return;
    this.screening.set(true);
    this.screeningMsg.set('');
    this.screeningProgress.set(0);

    // Animate progress bar while waiting
    this.screeningInterval = setInterval(() => {
      const current = this.screeningProgress();
      if (current < 90) {
        this.screeningProgress.set(current + Math.random() * 8);
      }
    }, 200);

    this.jobsApi.screenResumes(this.jobId).subscribe({
      next: (res) => {
        this.stopScreeningAnim();
        this.screeningProgress.set(100);
        this.screening.set(false);
        this.screeningMsg.set(res.message);
        this.loadResumes();
        this.loadRankings();
      },
      error: (err) => {
        this.stopScreeningAnim();
        this.screening.set(false);
        this.screeningMsg.set(err?.error?.message ?? 'Screening failed.');
      },
    });
  }

  private stopScreeningAnim(): void {
    if (this.screeningInterval) {
      clearInterval(this.screeningInterval);
      this.screeningInterval = null;
    }
  }

  // ── Resume Detail ──────────────────────────────────────────────────────
  openResumeDetail(resumeId: number): void {
    this.resumeDetailLoading.set(true);
    this.selectedResume.set(null);
    this.showResumeModal.set(true);
    this.hrStatusMsg.set('');
    this.jobsApi.getResumeDetail(resumeId).subscribe({
      next: (detail) => {
        this.selectedResume.set(detail);
        this.hrStatusValue = detail.hrStatus ?? 'Pending';
        this.hrNotesValue = detail.notes ?? '';
        this.resumeDetailLoading.set(false);
      },
      error: () => {
        this.resumeDetailLoading.set(false);
        this.showResumeModal.set(false);
      },
    });
  }

  closeResumeModal(): void {
    this.showResumeModal.set(false);
    this.selectedResume.set(null);
    this.hrStatusMsg.set('');
  }

  saveHrStatus(): void {
    const detail = this.selectedResume();
    if (!detail || !this.auth.isHrAdmin()) return;
    this.hrStatusSaving.set(true);
    this.hrStatusMsg.set('');
    this.jobsApi.updateResumeStatus(detail.id, this.hrStatusValue, this.hrNotesValue || null).subscribe({
      next: () => {
        this.hrStatusSaving.set(false);
        this.hrStatusMsg.set('Status saved.');
        // Update local state so rankings table reflects the change immediately
        this.loadRankings();
      },
      error: (err) => {
        this.hrStatusSaving.set(false);
        this.hrStatusMsg.set(err?.error?.message ?? 'Save failed.');
      },
    });
  }

  exportToExcel(): void {
    if (!this.auth.isHrAdmin()) return;
    this.exporting.set(true);
    this.jobsApi.exportRankings(this.jobId).subscribe({
      next: (blob) => {
        this.exporting.set(false);
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `rankings_job${this.jobId}_${new Date().toISOString().slice(0, 10)}.xlsx`;
        a.click();
        URL.revokeObjectURL(url);
      },
      error: () => {
        this.exporting.set(false);
      },
    });
  }

  getScoreBreakdown(): { label: string; value: number }[] {
    const detail = this.selectedResume();
    if (!detail?.scoreBreakdownJson) return [];
    try {
      const obj = JSON.parse(detail.scoreBreakdownJson) as Record<string, number>;
      return Object.entries(obj).map(([key, value]) => ({
        label: key.replace(/([A-Z])/g, ' $1').trim(),
        value,
      }));
    } catch {
      return [];
    }
  }

  getMatchedKeywordsList(): string[] {
    const detail = this.selectedResume();
    if (!detail?.matchedKeywords) return [];
    return detail.matchedKeywords.split(',').map((k) => k.trim()).filter((k) => k.length > 0);
  }

  onDragOver(ev: DragEvent): void {
    ev.preventDefault();
    ev.stopPropagation();
  }

  onResumeDrop(ev: DragEvent): void {
    ev.preventDefault();
    ev.stopPropagation();
    if (!this.auth.isHrAdmin()) return;
    const list = ev.dataTransfer?.files;
    if (list?.length) this.addResumeFiles(Array.from(list));
  }

  onResumeInput(ev: Event): void {
    if (!this.auth.isHrAdmin()) return;
    const input = ev.target as HTMLInputElement;
    const list = input.files;
    if (list?.length) this.addResumeFiles(Array.from(list));
    input.value = '';
  }

  onMyResumeFile(ev: Event): void {
    if (!this.auth.isViewer()) return;
    const input = ev.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    if (!file.name.toLowerCase().endsWith('.pdf')) {
      this.resumeMsg.set('Please choose a PDF file.');
      return;
    }
    this.resumeMsg.set('');
    this.myResumeBusy.set(true);
    this.jobsApi.uploadMyResume(this.jobId, file).subscribe({
      next: () => {
        this.myResumeBusy.set(false);
        this.resumeMsg.set('Your resume was saved.');
        this.loadResumes();
      },
      error: (err) => {
        this.myResumeBusy.set(false);
        this.resumeMsg.set(err?.error?.message ?? 'Upload failed.');
      },
    });
  }

  addResumeFiles(files: File[]): void {
    this.resumePickHint.set('');
    const pdfs = files.filter((f) => f.name.toLowerCase().endsWith('.pdf'));
    if (pdfs.length < files.length) {
      this.resumePickHint.set('Only PDF files are accepted; non-PDF files were skipped.');
    }
    if (pdfs.length === 0) {
      if (!this.resumePickHint()) this.resumePickHint.set('Choose one or more PDF resumes.');
      return;
    }
    const merged = [...this.selectedResumeFiles, ...pdfs];
    if (merged.length > 20) {
      this.resumePickHint.set('You can queue at most 20 PDFs per upload. Trim the list and try again.');
      this.selectedResumeFiles = merged.slice(0, 20);
      return;
    }
    this.selectedResumeFiles = merged;
  }

  removeQueuedResume(index: number): void {
    this.selectedResumeFiles = this.selectedResumeFiles.filter((_, i) => i !== index);
    this.resumePickHint.set('');
  }

  clearResumeQueue(): void {
    this.selectedResumeFiles = [];
    this.resumePickHint.set('');
  }

  private formatResumeUploadMessage(res: BulkResumeUploadResponse): string {
    const { uploadedCount, failedCount, results } = res;
    if (failedCount <= 0) return `Uploaded ${uploadedCount} resume(s).`;
    const failures = results
      .filter((r) => r.status === 'Failed')
      .map((r) => `${r.fileName}: ${r.message ?? 'Unknown error'}`);
    const detail = failures.length ? ` — ${failures.join('; ')}` : '';
    return `Finished: ${uploadedCount} uploaded, ${failedCount} failed.${detail}`;
  }

  uploadQueuedResumes(): void {
    if (!this.auth.isHrAdmin() || this.selectedResumeFiles.length === 0) return;
    this.resumeMsg.set('');
    this.resumeUploading.set(true);
    this.jobsApi.uploadResumes(this.jobId, this.selectedResumeFiles).subscribe({
      next: (res) => {
        this.resumeUploading.set(false);
        this.selectedResumeFiles = [];
        this.resumeMsg.set(this.formatResumeUploadMessage(res));
        this.loadResumes();
      },
      error: (err) => {
        this.resumeUploading.set(false);
        this.resumeMsg.set(err?.error?.message ?? 'Resume upload failed.');
      },
    });
  }

  save(): void {
    if (!this.auth.isHrAdmin() || this.editForm.invalid) return;
    this.saveMsg.set('');
    this.saving = true;
    const v = this.editForm.getRawValue();
    this.jobsApi
      .update(this.jobId, {
        title: v.title.trim(),
        description: v.description.trim(),
        status: v.status,
      })
      .subscribe({
        next: (j) => {
          this.job.set(j);
          this.saving = false;
          this.saveMsg.set('Saved.');
        },
        error: (err) => {
          this.saving = false;
          this.saveMsg.set(err?.error?.message ?? 'Save failed.');
        },
      });
  }

  onJdFile(ev: Event): void {
    if (!this.auth.isHrAdmin()) return;
    const input = ev.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    this.jdMsg.set('');
    this.jdUploading = true;
    this.jobsApi.replaceJd(this.jobId, file).subscribe({
      next: (j) => {
        this.job.set(j);
        this.jdUploading = false;
        this.jdMsg.set('Job description file updated.');
      },
      error: (err) => {
        this.jdUploading = false;
        this.jdMsg.set(err?.error?.message ?? 'Upload failed.');
      },
    });
  }

  deleteResumeRow(r: ResumeListItem): void {
    if (!this.auth.isHrAdmin() && !this.auth.isViewer()) return;
    if (!confirm(`Remove this resume (${r.candidateName})? The file will be deleted permanently.`)) return;
    this.deletingResumeId.set(r.id);
    this.resumeMsg.set('');
    this.jobsApi.deleteResume(this.jobId, r.id).subscribe({
      next: () => {
        this.deletingResumeId.set(null);
        this.resumeMsg.set('Resume removed.');
        this.loadResumes();
      },
      error: (err) => {
        this.deletingResumeId.set(null);
        this.resumeMsg.set(err?.error?.message ?? 'Could not delete resume.');
      },
    });
  }

  removeJob(): void {
    if (!this.auth.isHrAdmin()) return;
    if (!confirm('Delete this job permanently? This is only allowed if no resumes are attached.')) return;
    this.deleting = true;
    this.jobsApi.delete(this.jobId).subscribe({
      next: () => void this.router.navigate(['/jobs']),
      error: (err) => {
        this.deleting = false;
        alert(err?.error?.message ?? 'Could not delete job.');
      },
    });
  }
}
