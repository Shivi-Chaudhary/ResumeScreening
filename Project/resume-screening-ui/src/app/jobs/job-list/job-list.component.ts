import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { JobsService, JobListItem } from '../../core/jobs.service';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-job-list',
  standalone: true,
  imports: [FormsModule, RouterLink, DatePipe],
  templateUrl: './job-list.component.html',
  styleUrl: './job-list.component.scss',
})
export class JobListComponent implements OnInit {
  private readonly jobsApi = inject(JobsService);
  readonly auth = inject(AuthService);

  readonly jobs = signal<JobListItem[]>([]);
  readonly loading = signal(true);
  readonly error = signal('');

  statusFilter: 'All' | 'Active' | 'Closed' = 'All';

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.jobsApi.list(this.statusFilter).subscribe({
      next: (rows) => {
        this.jobs.set(rows);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err?.error?.message ?? 'Could not load jobs.');
      },
    });
  }

  onFilterChange(): void {
    this.load();
  }
}
