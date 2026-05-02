import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { JobsService, JobDetail, ResumeListItem, BulkResumeUploadResponse } from '../../core/jobs.service';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-job-detail',
  standalone: true,
  imports: [RouterLink, ReactiveFormsModule, DatePipe],
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
  /** Resume row id being deleted (disables that row’s button) */
  readonly deletingResumeId = signal<number | null>(null);

  /** Queued PDFs before upload (max 20) */
  selectedResumeFiles: File[] = [];

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
