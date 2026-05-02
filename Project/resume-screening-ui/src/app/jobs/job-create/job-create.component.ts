import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { of, switchMap } from 'rxjs';
import { JobsService } from '../../core/jobs.service';

@Component({
  selector: 'app-job-create',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './job-create.component.html',
  styleUrl: './job-create.component.scss',
})
export class JobCreateComponent {
  private readonly fb = inject(FormBuilder);
  private readonly jobsApi = inject(JobsService);
  private readonly router = inject(Router);

  readonly form = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    description: ['', [Validators.required]],
  });

  jdFile: File | null = null;
  errorMsg = '';
  loading = false;

  onFile(ev: Event): void {
    const input = ev.target as HTMLInputElement;
    this.jdFile = input.files?.[0] ?? null;
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.errorMsg = '';
    this.loading = true;

    const { title, description } = this.form.getRawValue();
    const file = this.jdFile;

    this.jobsApi
      .create({ title: title.trim(), description: description.trim() })
      .pipe(
        switchMap((job) =>
          file ? this.jobsApi.replaceJd(job.id, file) : of(job),
        ),
      )
      .subscribe({
        next: (job) => void this.router.navigate(['/jobs', job.id]),
        error: (err) => {
          this.loading = false;
          this.errorMsg = err?.error?.message ?? 'Could not create job.';
        },
      });
  }
}
