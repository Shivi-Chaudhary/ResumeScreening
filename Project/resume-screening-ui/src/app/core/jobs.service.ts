import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface JobListItem {
  id: number;
  title: string;
  status: string;
  createdAt: string;
  createdByUserId: number;
  createdByFullName: string;
  hasJdFile: boolean;
}

export interface JobDetail {
  id: number;
  title: string;
  description: string;
  jdFileUrl: string | null;
  jdExtractedText: string | null;
  status: string;
  createdAt: string;
  createdByUserId: number;
  createdByFullName: string;
}

export interface ResumeListItem {
  id: number;
  candidateName: string;
  candidateEmail: string | null;
  fileUrl: string;
  status: string;
  uploadedAt: string;
  hasExtractedText: boolean;
}

export interface ResumeUploadResult {
  fileName: string;
  resumeId: number;
  candidateName: string;
  candidateEmail: string | null;
  status: string;
  message: string | null;
}

export interface BulkResumeUploadResponse {
  jobId: number;
  totalFiles: number;
  uploadedCount: number;
  failedCount: number;
  results: ResumeUploadResult[];
}

export interface ScreeningResponse {
  jobId: number;
  resumesScored: number;
  message: string;
}

export interface RankedCandidate {
  rank: number;
  resumeId: number;
  candidateName: string;
  candidateEmail: string | null;
  score: number;
  scoreCategory: 'green' | 'amber' | 'red';
  matchedKeywords: string | null;
  fileUrl: string | null;
  scoredAt: string;
  hrStatus: string | null;
  notes: string | null;
}

export interface ResumeDetail {
  id: number;
  candidateName: string;
  candidateEmail: string | null;
  fileUrl: string;
  extractedText: string | null;
  status: string;
  uploadedAt: string;
  score: number | null;
  scoreCategory: string | null;
  matchedKeywords: string | null;
  scoreBreakdownJson: string | null;
  scoredAt: string | null;
  hrStatus: string | null;
  notes: string | null;
}

@Injectable({ providedIn: 'root' })
export class JobsService {
  private readonly http = inject(HttpClient);

  list(status: 'All' | 'Active' | 'Closed' = 'All'): Observable<JobListItem[]> {
    const params = new HttpParams().set('status', status);
    return this.http.get<JobListItem[]>('/api/jobs', { params });
  }

  get(id: number): Observable<JobDetail> {
    return this.http.get<JobDetail>(`/api/jobs/${id}`);
  }

  create(body: { title: string; description: string }): Observable<JobDetail> {
    return this.http.post<JobDetail>('/api/jobs', {
      title: body.title,
      description: body.description,
    });
  }

  update(
    id: number,
    body: { title?: string; description?: string; status?: string },
  ): Observable<JobDetail> {
    return this.http.put<JobDetail>(`/api/jobs/${id}`, body);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`/api/jobs/${id}`);
  }

  replaceJd(id: number, file: File): Observable<JobDetail> {
    const fd = new FormData();
    fd.append('file', file);
    return this.http.post<JobDetail>(`/api/jobs/${id}/jd`, fd);
  }

  listResumes(jobId: number): Observable<ResumeListItem[]> {
    return this.http.get<ResumeListItem[]>(`/api/jobs/${jobId}/resumes`);
  }

  /** Up to 20 PDFs; form field name must match API: `files` */
  uploadResumes(jobId: number, files: File[]): Observable<BulkResumeUploadResponse> {
    const fd = new FormData();
    for (const f of files) {
      fd.append('files', f, f.name);
    }
    return this.http.post<BulkResumeUploadResponse>(`/api/jobs/${jobId}/resumes`, fd);
  }

  /** Viewer: submit or replace own PDF for this job (single file, field name `file`) */
  uploadMyResume(jobId: number, file: File): Observable<ResumeListItem> {
    const fd = new FormData();
    fd.append('file', file, file.name);
    return this.http.post<ResumeListItem>(`/api/jobs/${jobId}/my-resume`, fd);
  }

  deleteResume(jobId: number, resumeId: number): Observable<void> {
    return this.http.delete<void>(`/api/jobs/${jobId}/resumes/${resumeId}`);
  }

  /** Trigger AI screening for all resumes under a job */
  screenResumes(jobId: number): Observable<ScreeningResponse> {
    return this.http.post<ScreeningResponse>(`/api/jobs/${jobId}/screen`, {});
  }

  /** Get ranked candidates sorted by score descending */
  getRankings(jobId: number): Observable<RankedCandidate[]> {
    return this.http.get<RankedCandidate[]>(`/api/jobs/${jobId}/rankings`);
  }

  /** Get resume detail with full score breakdown */
  getResumeDetail(resumeId: number): Observable<ResumeDetail> {
    return this.http.get<ResumeDetail>(`/api/resumes/${resumeId}`);
  }

  /** HRAdmin: update shortlist / reject / review decision on a resume */
  updateResumeStatus(resumeId: number, hrStatus: string, notes: string | null): Observable<void> {
    return this.http.put<void>(`/api/resumes/${resumeId}/status`, { hrStatus, notes });
  }

  /** HRAdmin: download ranked candidates as Excel file */
  exportRankings(jobId: number): Observable<Blob> {
    return this.http.get(`/api/jobs/${jobId}/rankings/export`, { responseType: 'blob' });
  }
}
